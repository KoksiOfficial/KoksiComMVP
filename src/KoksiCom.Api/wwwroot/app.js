(function () {
  const TOKEN_KEY = "koksicom.jwt";
  const ICE_SERVERS = [{ urls: "stun:stun.l.google.com:19302" }];
  const AUDIO_CONSTRAINTS = {
    audio: {
      echoCancellation: true,
      noiseSuppression: true,
      autoGainControl: true,
      channelCount: 1
    },
    video: false
  };

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
  const usersBody = document.querySelector("#users-tbl tbody");
  const micBtn = document.getElementById("mic-btn");
  const headphonesBtn = document.getElementById("headphones-btn");
  const shareBtn = document.getElementById("share-btn");
  const stopShareBtn = document.getElementById("stopShare-btn");
  const inboundBox = document.getElementById("inbound-broadcast");
  const inboundNameEl = document.getElementById("inbound-name");
  const inboundVideo = document.getElementById("inbound-video");
  const inboundVolume = document.querySelector("#broadcast-area input");
  const inboundExtend = document.querySelector("#broadcast-area button");
  const chatList = document.getElementById("chat-list");
  const chatInput = document.querySelector("#chat input");
  const chatSubmit = document.querySelector("#chat button");
  const stunIpEl = document.getElementById("stun-ip");
  const statsBodyHost = document.getElementById("stats-tbl");
  const remoteSdpEl = document.getElementById("remote-sdp");
  const localSdpEl = document.getElementById("local-sdp");

  let errorTimer = 0;
  let joinRoomName = "";
  let selfName = "";
  let roomGen = 0;
  let hub = null;
  let leavingIntentionally = false;
  let iceFailedShown = false;
  let statsTimer = 0;
  let micOn = false;
  let headphonesOn = true;
  let shareOn = false;
  let localStream = null;
  let shareStream = null;
  let watchingName = "";
  let stunPc = null;
  let stunIp = "";
  const roster = new Map();
  const peers = new Map();

  function logBrowserError(context, error) {
    console.error(context, error);
    console.error(context + " name:", error && error.name);
    console.error(context + " message:", error && error.message);
    console.error(context + " stack:", error && error.stack);
    console.error(context + " context:", {
      isSecureContext: window.isSecureContext,
      origin: location.origin,
      protocol: location.protocol,
      mediaDevices: !!navigator.mediaDevices,
      getUserMedia: !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia),
      getDisplayMedia: !!(navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia)
    });
  }

  function srflxAddress(candidate) {
    if (!candidate) {
      return "";
    }
    if (candidate.type && candidate.type !== "srflx") {
      return "";
    }
    if (candidate.type === "srflx") {
      const fromField = candidate.address || candidate.ip || "";
      if (fromField) {
        return fromField;
      }
    }
    const raw = candidate.candidate || "";
    const parts = raw.split(" ");
    const typ = parts.indexOf("typ");
    if (typ >= 2 && parts[typ + 1] === "srflx") {
      return parts[typ - 2] || "";
    }
    return "";
  }

  function setStunIp(value) {
    if (!value) {
      return;
    }
    stunIp = value;
    stunIpEl.textContent = value;
  }

  async function startStunLookup() {
    stopStunLookup();
    stunIp = "";
    stunIpEl.textContent = "";
    try {
      stunPc = new RTCPeerConnection({ iceServers: ICE_SERVERS });
      stunPc.createDataChannel("stun");
      stunPc.onicecandidate = function (event) {
        setStunIp(srflxAddress(event.candidate));
      };
      stunPc.onicegatheringstatechange = async function () {
        if (!stunPc || stunPc.iceGatheringState !== "complete" || stunIp) {
          return;
        }
        try {
          const stats = await stunPc.getStats();
          stats.forEach(function (report) {
            if (report.type === "local-candidate" && report.candidateType === "srflx") {
              setStunIp(report.address || report.ip || report.ipAddress || "");
            }
          });
        } catch (e) {
          logBrowserError("STUN getStats", e);
        }
      };
      await stunPc.setLocalDescription(await stunPc.createOffer());
    } catch (e) {
      logBrowserError("STUN lookup", e);
    }
  }

  function stopStunLookup() {
    if (stunPc) {
      try {
        stunPc.close();
      } catch (e) { /* already closed */ }
      stunPc = null;
    }
  }

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

  function cellText(on) {
    return on ? "on" : "off";
  }

  function renderUsers() {
    usersBody.replaceChildren();
    roster.forEach(function (info, name) {
      const tr = document.createElement("tr");
      tr.dataset.name = name;
      ["name", "mic", "headphones", "share"].forEach(function (key) {
        const td = document.createElement("td");
        td.textContent = key === "name" ? name : cellText(info[key]);
        tr.appendChild(td);
      });
      usersBody.appendChild(tr);
    });
  }

  function upsertPeerState(info) {
    roster.set(info.name, {
      mic: !!info.mic,
      headphones: !!info.headphones,
      share: !!info.share
    });
    renderUsers();
    if (watchingName === info.name && !info.share) {
      hideInbound();
    }
    applyHeadphones();
  }

  function hideInbound() {
    watchingName = "";
    inboundBox.hidden = true;
    inboundNameEl.textContent = "";
    inboundVideo.srcObject = null;
  }

  function videoOnlyStream(session) {
    const stream = new MediaStream();
    if (!session || !session.stream) {
      return stream;
    }
    session.stream.getVideoTracks().forEach(function (track) {
      stream.addTrack(track);
    });
    return stream;
  }

  function showInbound(name) {
    const session = peers.get(name);
    watchingName = name;
    inboundBox.hidden = false;
    inboundNameEl.textContent = name;
    inboundVideo.srcObject = videoOnlyStream(session);
    inboundVideo.volume = Number(inboundVolume.value) / 100;
  }

  function focusedPeer() {
    if (watchingName && peers.has(watchingName)) {
      return peers.get(watchingName);
    }
    const first = peers.values().next();
    return first.done ? null : first.value;
  }

  function reportValue(report, key) {
    const value = report[key];
    return value === undefined || value === null ? "" : String(value);
  }

  async function refreshDashboard() {
    stunIpEl.textContent = stunIp;
    const session = focusedPeer();
    if (!session) {
      statsBodyHost.replaceChildren();
      localSdpEl.value = "";
      remoteSdpEl.value = "";
      return;
    }

    const pc = session.pc;
    localSdpEl.value = pc.localDescription ? pc.localDescription.sdp || "" : "";
    remoteSdpEl.value = pc.remoteDescription ? pc.remoteDescription.sdp || "" : "";

    const table = document.createElement("tbody");
    function addRow(label, value) {
      const tr = document.createElement("tr");
      const k = document.createElement("td");
      const v = document.createElement("td");
      k.textContent = label;
      v.textContent = value;
      tr.appendChild(k);
      tr.appendChild(v);
      table.appendChild(tr);
    }

    addRow("connectionState", pc.connectionState);
    addRow("iceConnectionState", pc.iceConnectionState);
    addRow("signalingState", pc.signalingState);

    try {
      const stats = await pc.getStats();
      stats.forEach(function (report) {
        if (report.type === "local-candidate" && report.candidateType === "srflx") {
          setStunIp(report.address || report.ip || report.ipAddress || "");
        }
        const parts = [report.type];
        ["id", "state", "nominated", "candidateType", "address", "port", "bytesSent", "bytesReceived", "packetsLost"]
          .forEach(function (key) {
            const value = reportValue(report, key);
            if (value) {
              parts.push(key + "=" + value);
            }
          });
        addRow(report.type || "stat", parts.join(" "));
      });
    } catch (e) {
      addRow("getStats", "unavailable");
    }

    statsBodyHost.replaceChildren(table);
  }

  function bindChatChannel(channel) {
    channel.onmessage = function (event) {
      let data = null;
      try {
        data = JSON.parse(event.data);
      } catch (e) {
        return;
      }
      if (!data || !data.name || typeof data.body !== "string") {
        return;
      }
      appendChat(data.name, data.body);
    };
  }

  function appendChat(name, body) {
    const li = document.createElement("li");
    li.textContent = name + ": " + body;
    chatList.appendChild(li);
  }

  function sendChat(body) {
    const payload = JSON.stringify({ name: selfName, body: body });
    peers.forEach(function (session) {
      const channel = session.channel;
      if (channel && channel.readyState === "open") {
        channel.send(payload);
      }
    });
    appendChat(selfName, body);
  }

  function addLocalTracks(pc) {
    if (localStream) {
      localStream.getTracks().forEach(function (track) {
        if (!pc.getSenders().some(function (sender) { return sender.track === track; })) {
          pc.addTrack(track, localStream);
        }
      });
    }
    if (shareStream) {
      shareStream.getTracks().forEach(function (track) {
        if (!pc.getSenders().some(function (sender) { return sender.track === track; })) {
          pc.addTrack(track, shareStream);
        }
      });
    }
  }

  function applyHeadphones() {
    peers.forEach(function (session) {
      if (!session.audio) {
        return;
      }
      const info = roster.get(session.name);
      const remoteMicOn = !!(info && info.mic);
      session.audio.muted = !headphonesOn || !remoteMicOn;
    });
  }

  function closePeer(name) {
    const session = peers.get(name);
    if (!session) {
      return;
    }
    try {
      session.pc.close();
    } catch (e) { /* already closed */ }
    if (session.audio && session.audio.parentNode) {
      session.audio.remove();
    }
    peers.delete(name);
    if (watchingName === name) {
      hideInbound();
    }
  }

  function ensurePeer(remoteName) {
    if (peers.has(remoteName)) {
      return peers.get(remoteName);
    }

    const polite = selfName > remoteName;
    const pc = new RTCPeerConnection({ iceServers: ICE_SERVERS });
    const session = {
      name: remoteName,
      pc: pc,
      polite: polite,
      makingOffer: false,
      ignoreOffer: false,
      channel: null,
      stream: new MediaStream(),
      audio: document.createElement("audio")
    };
    session.audio.autoplay = true;
    session.audio.playsInline = true;
    session.audio.muted = true;
    session.audio.hidden = true;
    document.body.appendChild(session.audio);

    pc.onicecandidate = function (event) {
      if (!event.candidate || !hub) {
        return;
      }
      hub.invoke("Signal", remoteName, { candidate: event.candidate.toJSON() }).catch(function () {
        showError("Request failed.");
      });
    };

    pc.onnegotiationneeded = async function () {
      try {
        session.makingOffer = true;
        await pc.setLocalDescription();
        if (hub) {
          await hub.invoke("Signal", remoteName, {
            description: { type: pc.localDescription.type, sdp: pc.localDescription.sdp }
          });
        }
      } catch (e) {
        showError("Request failed.");
      } finally {
        session.makingOffer = false;
      }
    };

    pc.onconnectionstatechange = function () {
      if (pc.connectionState === "failed" && !iceFailedShown) {
        iceFailedShown = true;
        showError("Failed to connect to the room.");
      }
      refreshDashboard();
    };

    pc.oniceconnectionstatechange = function () {
      if (pc.iceConnectionState === "failed" && !iceFailedShown) {
        iceFailedShown = true;
        showError("Failed to connect to the room.");
      }
    };

    pc.ontrack = function (event) {
      if (!session.stream.getTracks().includes(event.track)) {
        session.stream.addTrack(event.track);
      }
      if (event.track.kind === "audio") {
        session.audio.srcObject = new MediaStream([event.track]);
        applyHeadphones();
      }
      if (watchingName === remoteName && event.track.kind === "video") {
        inboundVideo.srcObject = videoOnlyStream(session);
      }
      event.track.addEventListener("ended", function () {
        session.stream.removeTrack(event.track);
        if (watchingName === remoteName && event.track.kind === "video") {
          hideInbound();
        }
      });
    };

    pc.ondatachannel = function (event) {
      session.channel = event.channel;
      bindChatChannel(session.channel);
    };

    if (!polite) {
      session.channel = pc.createDataChannel("chat");
      bindChatChannel(session.channel);
    }

    addLocalTracks(pc);
    peers.set(remoteName, session);
    applyHeadphones();
    return session;
  }

  async function handleSignal(fromName, payload) {
    if (!payload || fromName === selfName) {
      return;
    }
    const session = ensurePeer(fromName);
    const pc = session.pc;
    try {
      if (payload.description) {
        const description = payload.description;
        const offerCollision = description.type === "offer"
          && (session.makingOffer || pc.signalingState !== "stable");
        session.ignoreOffer = !session.polite && offerCollision;
        if (session.ignoreOffer) {
          return;
        }
        await pc.setRemoteDescription(description);
        if (description.type === "offer") {
          await pc.setLocalDescription();
          if (hub) {
            await hub.invoke("Signal", fromName, {
              description: { type: pc.localDescription.type, sdp: pc.localDescription.sdp }
            });
          }
        }
      } else if (payload.candidate) {
        try {
          await pc.addIceCandidate(payload.candidate);
        } catch (e) {
          if (!session.ignoreOffer) {
            throw e;
          }
        }
      }
    } catch (e) {
      showError("Request failed.");
    }
  }

  async function stopTracks(stream) {
    if (!stream) {
      return;
    }
    stream.getTracks().forEach(function (track) {
      track.stop();
    });
  }

  async function stopSharing() {
    if (shareStream) {
      shareStream.getTracks().forEach(function (track) {
        track.stop();
        peers.forEach(function (session) {
          session.pc.getSenders().forEach(function (sender) {
            if (sender.track === track) {
              session.pc.removeTrack(sender);
            }
          });
        });
      });
      shareStream = null;
    }
    shareOn = false;
    stopShareBtn.hidden = true;
    if (roster.has(selfName)) {
      roster.get(selfName).share = false;
      renderUsers();
    }
    if (hub) {
      try {
        await hub.invoke("SetState", micOn, headphonesOn, false);
      } catch (e) {
        showError("Request failed.");
      }
    }
  }

  async function teardownRoomMedia() {
    hideInbound();
    Array.from(peers.keys()).forEach(closePeer);
    await stopTracks(localStream);
    localStream = null;
    await stopTracks(shareStream);
    shareStream = null;
    shareOn = false;
    micOn = false;
    headphonesOn = true;
    iceFailedShown = false;
    stopShareBtn.hidden = true;
    roster.clear();
    usersBody.replaceChildren();
    chatList.replaceChildren();
    stopStunLookup();
    stunIp = "";
    stunIpEl.textContent = "";
    statsBodyHost.replaceChildren();
    localSdpEl.value = "";
    remoteSdpEl.value = "";
    if (statsTimer) {
      clearInterval(statsTimer);
      statsTimer = 0;
    }
  }

  async function stopHub(intentional) {
    leavingIntentionally = intentional;
    if (!hub) {
      return;
    }
    const connection = hub;
    hub = null;
    if (intentional) {
      try {
        await connection.invoke("Leave");
      } catch (e) { /* already gone */ }
    }
    try {
      await connection.stop();
    } catch (e) { /* already stopped */ }
  }

  async function enterRoom(name) {
    const gen = ++roomGen;
    leavingIntentionally = false;
    iceFailedShown = false;
    micOn = false;
    headphonesOn = true;
    shareOn = false;
    stopShareBtn.hidden = true;
    hideInbound();
    hideRoomForms();
    roomTitle.textContent = name;
    showPage("room");

    if (!selfName) {
      const me = await api("GET", "/api/guest/me");
      if (me.status === 200 && me.data && me.data.name) {
        selfName = me.data.name;
      }
    }

    startStunLookup();

    if (gen !== roomGen) {
      return;
    }

    hub = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/room", { accessTokenFactory: function () { return token(); } })
      .build();

    hub.on("peers", function (list) {
      roster.clear();
      (list || []).forEach(function (info) {
        upsertPeerState(info);
        if (info.name !== selfName) {
          ensurePeer(info.name);
        }
      });
      renderUsers();
    });

    hub.on("peerJoined", function (info) {
      upsertPeerState(info);
      if (info.name !== selfName) {
        ensurePeer(info.name);
      }
    });

    hub.on("peerLeft", function (leftName) {
      roster.delete(leftName);
      renderUsers();
      closePeer(leftName);
    });

    hub.on("peerState", function (peerName, state) {
      upsertPeerState({
        name: peerName,
        mic: state && state.mic,
        headphones: state && state.headphones,
        share: state && state.share
      });
    });

    hub.on("signal", function (fromName, payload) {
      handleSignal(fromName, payload);
    });

    hub.onclose(function () {
      if (leavingIntentionally || gen !== roomGen) {
        return;
      }
      roomGen += 1;
      teardownRoomMedia();
      showPage("login");
      showError("Request failed.");
    });

    try {
      await hub.start();
      await hub.invoke("Enter");
    } catch (e) {
      showError("Request failed.");
      return;
    }

    if (gen !== roomGen) {
      return;
    }

    statsTimer = window.setInterval(refreshDashboard, 1000);
    refreshDashboard();
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
    selfName = me.data && me.data.name ? me.data.name : "";
    const room = await api("GET", "/api/rooms/me");
    if (room.status === 200 && room.data && room.data.name) {
      await enterRoom(room.data.name);
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
        selfName = name;
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
    selfName = "";
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
    let created = false;
    try {
      const result = await api("POST", "/api/rooms", { name: name, pwd: pwd });
      if (result.status === 200) {
        created = true;
      } else if (result.status === 400 && result.data && result.data.error === "taken") {
        showError("That room name is taken. Choose another.");
      } else if (result.status === 400 && result.data && result.data.error === "invalid") {
        showError("Room name must be 2 to 10 English letters or digits, with no spaces.");
      } else {
        showError("Request failed.");
      }
    } catch (e) {
      showError("Request failed.");
    } finally {
      setFormLoading(createForm, false);
    }
    if (created) {
      await enterRoom(name);
    }
  });

  formButton(enterForm).addEventListener("click", async function () {
    const pwd = enterForm.elements.namedItem("pwd").value;
    if (!isValidPassword(pwd)) {
      showError("Password must be 6 to 10 characters and include a number, an uppercase letter, and a lowercase letter.");
      return;
    }
    setFormLoading(enterForm, true);
    let joined = false;
    try {
      const result = await api("POST", "/api/rooms/join", { name: joinRoomName, pwd: pwd });
      if (result.status === 200) {
        joined = true;
      } else if (result.status === 403) {
        showError("Incorrect password.");
      } else if (result.status === 400 && result.data && result.data.error === "invalid") {
        showError("Password must be 6 to 10 characters and include a number, an uppercase letter, and a lowercase letter.");
      } else {
        showError("Request failed.");
      }
    } catch (e) {
      showError("Request failed.");
    } finally {
      setFormLoading(enterForm, false);
    }
    if (joined) {
      await enterRoom(joinRoomName);
    }
  });

  leaveBtn.addEventListener("click", async function () {
    const gen = ++roomGen;
    await teardownRoomMedia();
    await stopHub(true);
    if (gen !== roomGen) {
      return;
    }
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

  function publishMicState() {
    if (roster.has(selfName)) {
      roster.get(selfName).mic = micOn;
      renderUsers();
    }
    if (hub) {
      hub.invoke("SetState", micOn, headphonesOn, shareOn).catch(function (e) {
        logBrowserError("SetState mic", e);
        showError("Request failed.");
      });
    }
  }

  function setMicEnabled(on) {
    micOn = on;
    if (localStream) {
      localStream.getAudioTracks().forEach(function (track) {
        track.enabled = on;
      });
    }
    publishMicState();
  }

  micBtn.addEventListener("click", function () {
    if (localStream) {
      setMicEnabled(!micOn);
      return;
    }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      const error = new Error("getUserMedia is not available (secureContext=" + window.isSecureContext + ", origin=" + location.origin + ")");
      logBrowserError("Microphone", error);
      showError("Request failed.");
      return;
    }

    navigator.mediaDevices.getUserMedia(AUDIO_CONSTRAINTS).then(function (stream) {
      localStream = stream;
      peers.forEach(function (session) {
        addLocalTracks(session.pc);
      });
      setMicEnabled(true);
    }).catch(function (e) {
      logBrowserError("Microphone", e);
      setMicEnabled(false);
      showError("Request failed.");
    });
  });

  headphonesBtn.addEventListener("click", async function () {
    headphonesOn = !headphonesOn;
    applyHeadphones();
    if (roster.has(selfName)) {
      roster.get(selfName).headphones = headphonesOn;
      renderUsers();
    }
    if (hub) {
      try {
        await hub.invoke("SetState", micOn, headphonesOn, shareOn);
      } catch (e) {
        showError("Request failed.");
      }
    }
  });

  shareBtn.addEventListener("click", function () {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
      const error = new Error("getDisplayMedia is not available (secureContext=" + window.isSecureContext + ", origin=" + location.origin + ")");
      logBrowserError("Share screen", error);
      showError("Request failed.");
      return;
    }

    navigator.mediaDevices.getDisplayMedia({ video: true, audio: false }).then(function (stream) {
      var startShare = function () {
        shareStream = stream;
        shareOn = true;
        stopShareBtn.hidden = false;
        stream.getVideoTracks().forEach(function (track) {
          track.addEventListener("ended", function () {
            if (shareStream === stream) {
              stopSharing();
            }
          });
        });
        peers.forEach(function (session) {
          addLocalTracks(session.pc);
        });
        if (roster.has(selfName)) {
          roster.get(selfName).share = true;
          renderUsers();
        }
        if (hub) {
          hub.invoke("SetState", micOn, headphonesOn, true).catch(function (e) {
            logBrowserError("SetState share", e);
            showError("Request failed.");
          });
        }
      };
      if (shareStream) {
        Promise.resolve(stopSharing()).then(startShare);
        return;
      }
      startShare();
    }).catch(function (e) {
      if (e && e.name === "AbortError") {
        return;
      }
      logBrowserError("Share screen", e);
      showError("Request failed.");
    });
  });

  stopShareBtn.addEventListener("click", function () {
    stopSharing();
  });

  usersBody.addEventListener("click", function (event) {
    const row = event.target.closest("tr");
    if (!row) {
      return;
    }
    const name = row.dataset.name;
    if (!name || name === selfName) {
      return;
    }
    const info = roster.get(name);
    if (!info || !info.share) {
      return;
    }
    showInbound(name);
    refreshDashboard();
  });

  inboundVolume.addEventListener("input", function () {
    inboundVideo.volume = Number(inboundVolume.value) / 100;
  });

  inboundExtend.addEventListener("click", function () {
    const target = inboundBox;
    if (document.fullscreenElement) {
      document.exitFullscreen();
      return;
    }
    if (target.requestFullscreen) {
      target.requestFullscreen();
    }
  });

  chatSubmit.addEventListener("click", function () {
    const body = chatInput.value;
    if (!body) {
      return;
    }
    chatInput.value = "";
    sendChat(body);
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
