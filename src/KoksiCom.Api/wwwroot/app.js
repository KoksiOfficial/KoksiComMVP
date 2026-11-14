(function () {
  const TOKEN_KEY = "koksicom.jwt";

  const errorEl = document.getElementById("error-msg");
  const loadingEl = document.getElementById("loading-msg");
  const pageLogin = document.getElementById("page-login");
  const pageList = document.getElementById("page-list");
  const pageRoom = document.getElementById("page-room");
  const loginForm = document.getElementById("logIn-frm");
  const roomsTable = document.getElementById("rooms-tbl");
  const roomsBody = roomsTable.querySelector("tbody");
  const enterForm = document.getElementById("enterRoom-frm");
  const createForm = document.getElementById("createRoom-frm");
  const createRoomBtn = document.getElementById("createRoom-btn");
  const logOutBtn = document.getElementById("logOut-btn");
  const enterTitle = document.getElementById("enter-room-title");
  const roomTitle = document.getElementById("room-title");
  const leaveBtn = document.getElementById("leave-btn");

  let errorTimer = 0;
  let joinRoomName = "";

  function isValidName(value) {
    return /^[A-Za-z0-9]{2,10}$/.test(value);
  }

  function isValidPassword(value) {
    return value.length >= 6 && value.length <= 10
      && /[0-9]/.test(value)
      && /[A-Z]/.test(value)
      && /[a-z]/.test(value);
  }

  function showError(message) {
    clearTimeout(errorTimer);
    errorEl.textContent = message;
    errorEl.hidden = false;
    errorEl.classList.remove("is-fading");
    errorEl.style.animation = "none";
    errorEl.offsetWidth;
    errorEl.style.animation = "";
    errorEl.classList.add("is-fading");
    errorTimer = window.setTimeout(function () {
      errorEl.hidden = true;
      errorEl.classList.remove("is-fading");
      errorEl.style.animation = "";
      errorEl.textContent = "";
    }, 5000);
  }

  function setFormLoading(form, loading) {
    if (form) {
      form.classList.toggle("is-loading", loading);
    }
    loadingEl.hidden = !loading;
  }

  function showPage(name) {
    pageLogin.classList.toggle("is-visible", name === "login");
    pageList.classList.toggle("is-visible", name === "list");
    pageRoom.classList.toggle("is-visible", name === "room");
  }

  function hideRoomForms() {
    enterForm.hidden = true;
    createForm.hidden = true;
  }

  function token() {
    return localStorage.getItem(TOKEN_KEY);
  }

  function setToken(value) {
    if (value) {
      localStorage.setItem(TOKEN_KEY, value);
    } else {
      localStorage.removeItem(TOKEN_KEY);
    }
  }

  async function api(method, path, body) {
    const headers = { Accept: "application/json" };
    const jwt = token();
    if (jwt) {
      headers.Authorization = "Bearer " + jwt;
    }
    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }
    const response = await fetch(path, {
      method: method,
      headers: headers,
      body: body !== undefined ? JSON.stringify(body) : undefined
    });
    const text = await response.text();
    let data = null;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch (e) {
        data = null;
      }
    }
    return { status: response.status, data: data };
  }

  function formButton(form) {
    return form.querySelector("button");
  }

  async function loadRooms() {
    const result = await api("GET", "/api/rooms");
    roomsBody.replaceChildren();
    if (result.status !== 200 || !result.data || !Array.isArray(result.data.rooms)) {
      showError("Request failed.");
      return;
    }
    result.data.rooms.forEach(function (room) {
      const tr = document.createElement("tr");
      const nameTd = document.createElement("td");
      const usersTd = document.createElement("td");
      nameTd.textContent = room.name;
      usersTd.textContent = String(room.users);
      tr.appendChild(nameTd);
      tr.appendChild(usersTd);
      tr.addEventListener("click", function () {
        joinRoomName = room.name;
        enterTitle.textContent = room.name;
        createForm.hidden = true;
        enterForm.hidden = false;
        enterForm.querySelector('[name="pwd"]').value = "";
      });
      roomsBody.appendChild(tr);
    });
  }

  async function showList() {
    hideRoomForms();
    showPage("list");
    await loadRooms();
  }

  function showRoom(name) {
    hideRoomForms();
    roomTitle.textContent = name;
    showPage("room");
  }

  async function start() {
    if (!token()) {
      showPage("login");
      return;
    }
    const me = await api("GET", "/api/guest/me");
    if (me.status !== 200) {
      setToken(null);
      showPage("login");
      return;
    }
    const room = await api("GET", "/api/rooms/me");
    if (room.status === 200 && room.data && room.data.name) {
      showRoom(room.data.name);
      return;
    }
    await showList();
  }

  formButton(loginForm).addEventListener("click", async function () {
    const name = loginForm.elements.namedItem("name").value;
    if (!isValidName(name)) {
      showError("Display name must be 2 to 10 English letters or digits, with no spaces.");
      return;
    }
    setFormLoading(loginForm, true);
    try {
      const result = await api("POST", "/api/guest/login", { name: name });
      if (result.status === 200 && result.data && result.data.token) {
        setToken(result.data.token);
        await showList();
        return;
      }
      if (result.status === 400 && result.data && result.data.error === "taken") {
        showError("That display name is taken.");
        return;
      }
      if (result.status === 400 && result.data && result.data.error === "invalid") {
        showError("Display name must be 2 to 10 English letters or digits, with no spaces.");
        return;
      }
      showError("Request failed.");
    } catch (e) {
      showError("Request failed.");
    } finally {
      setFormLoading(loginForm, false);
    }
  });

  createRoomBtn.addEventListener("click", function () {
    enterForm.hidden = true;
    createForm.hidden = false;
  });

  logOutBtn.addEventListener("click", function () {
    setToken(null);
    hideRoomForms();
    showPage("login");
  });

  formButton(createForm).addEventListener("click", async function () {
    const name = createForm.elements.namedItem("name").value;
    const pwd = createForm.elements.namedItem("pwd").value;
    if (!isValidName(name)) {
      showError("Room name must be 2 to 10 English letters or digits, with no spaces.");
      return;
    }
    if (!isValidPassword(pwd)) {
      showError("Password must be 6 to 10 characters and include a number, an uppercase letter, and a lowercase letter.");
      return;
    }
    setFormLoading(createForm, true);
    try {
      const result = await api("POST", "/api/rooms", { name: name, pwd: pwd });
      if (result.status === 200) {
        showRoom(name);
        return;
      }
      if (result.status === 400 && result.data && result.data.error === "taken") {
        showError("That room name is taken. Choose another.");
        return;
      }
      if (result.status === 400 && result.data && result.data.error === "invalid") {
        showError("Room name must be 2 to 10 English letters or digits, with no spaces.");
        return;
      }
      showError("Request failed.");
    } catch (e) {
      showError("Request failed.");
    } finally {
      setFormLoading(createForm, false);
    }
  });

  formButton(enterForm).addEventListener("click", async function () {
    const pwd = enterForm.elements.namedItem("pwd").value;
    if (!isValidPassword(pwd)) {
      showError("Password must be 6 to 10 characters and include a number, an uppercase letter, and a lowercase letter.");
      return;
    }
    setFormLoading(enterForm, true);
    try {
      const result = await api("POST", "/api/rooms/join", { name: joinRoomName, pwd: pwd });
      if (result.status === 200) {
        showRoom(joinRoomName);
        return;
      }
      if (result.status === 403) {
        showError("Incorrect password.");
        return;
      }
      if (result.status === 400 && result.data && result.data.error === "invalid") {
        showError("Password must be 6 to 10 characters and include a number, an uppercase letter, and a lowercase letter.");
        return;
      }
      showError("Request failed.");
    } catch (e) {
      showError("Request failed.");
    } finally {
      setFormLoading(enterForm, false);
    }
  });

  leaveBtn.addEventListener("click", async function () {
    try {
      const result = await api("POST", "/api/rooms/leave");
      if (result.status === 200) {
        await showList();
        return;
      }
      showError("Request failed.");
    } catch (e) {
      showError("Request failed.");
    }
  });

  document.addEventListener("click", function (event) {
    if (enterForm.hidden && createForm.hidden) {
      return;
    }
    if (event.target.closest("#enterRoom-frm")
      || event.target.closest("#createRoom-frm")
      || event.target.closest("#createRoom-btn")
      || event.target.closest("#logOut-btn")
      || event.target.closest("#rooms-tbl tbody tr")) {
      return;
    }
    hideRoomForms();
  });

  start();
})();
