mergeInto(LibraryManager.library, {
  EG_Connect: function (receiverNamePtr) {
    var receiverName = UTF8ToString(receiverNamePtr);
    if (typeof io !== "function") {
      SendMessage(receiverName, "OnDisconnected", "Socket.IO client is missing");
      return;
    }

    if (Module.easyGameSocket) {
      Module.easyGameSocket.disconnect();
    }

    var guestToken = null;
    try {
      guestToken = localStorage.getItem("easygame.guestToken");
    } catch (_) {}

    var socket = io({
      auth: { guestToken: guestToken },
      transports: ["polling", "websocket"],
      tryAllTransports: true,
      timeout: 12000
    });
    Module.easyGameSocket = socket;

    socket.on("connect", function () {
      SendMessage(receiverName, "OnConnected", "");
    });
    socket.on("disconnect", function (reason) {
      SendMessage(receiverName, "OnDisconnected", reason || "disconnected");
    });
    socket.on("connect_error", function (error) {
      SendMessage(receiverName, "OnDisconnected", error && error.message ? error.message : "connection failed");
    });
    socket.on("welcome", function (payload) {
      try {
        if (payload.guestToken) {
          localStorage.setItem("easygame.guestToken", payload.guestToken);
        }
      } catch (_) {}
      SendMessage(receiverName, "OnWelcome", JSON.stringify(payload));
    });
    socket.on("snapshot", function (payload) {
      SendMessage(receiverName, "OnSnapshot", JSON.stringify(payload));
    });
    socket.on("attack", function (payload) {
      SendMessage(receiverName, "OnAttack", JSON.stringify(payload));
    });
    socket.on("notification", function (payload) {
      SendMessage(receiverName, "OnNotification", JSON.stringify(payload));
    });
    socket.on("network:pong", function (payload) {
      SendMessage(receiverName, "OnPong", JSON.stringify(payload));
    });
  },

  EG_SendInput: function (jsonPtr) {
    if (Module.easyGameSocket && Module.easyGameSocket.connected) {
      Module.easyGameSocket.emit("input", JSON.parse(UTF8ToString(jsonPtr)));
    }
  },

  EG_SendPing: function (jsonPtr) {
    if (Module.easyGameSocket && Module.easyGameSocket.connected) {
      Module.easyGameSocket.emit("network:ping", JSON.parse(UTF8ToString(jsonPtr)));
    }
  },

  EG_Disconnect: function () {
    if (Module.easyGameSocket) {
      Module.easyGameSocket.disconnect();
      Module.easyGameSocket = null;
    }
  }
});
