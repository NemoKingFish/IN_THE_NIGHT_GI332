mergeInto(LibraryManager.library, {
  SocketCreate: function(url, protocols, openCallback, recvCallback, errorCallback, closeCallback) {
    var socketUrl = UTF8ToString(url);
    var socketProtocols = protocols ? UTF8ToString(protocols) : "";
    var protocolList = socketProtocols ? [socketProtocols] : undefined;

    if (!Module.PHOTON_WebSocketInstances) {
      Module.PHOTON_WebSocketInstances = [];
    }

    var instance = Module.PHOTON_WebSocketInstances.length;
    var entry = {
      socket: null,
      error: "",
      closeCode: 1000
    };

    var socket = protocolList ? new WebSocket(socketUrl, protocolList) : new WebSocket(socketUrl);
    entry.socket = socket;
    socket.binaryType = "arraybuffer";

    var invokeRecv = function(arrayBuffer) {
      var bytes = new Uint8Array(arrayBuffer);
      var buffer = _malloc(bytes.length);
      if (!buffer) {
        entry.error = "Failed to allocate memory for incoming websocket packet.";
        {{{ makeDynCall('vii', 'errorCallback') }}}(instance, 1006);
        return;
      }

      HEAPU8.set(bytes, buffer);
      {{{ makeDynCall('viii', 'recvCallback') }}}(instance, buffer, bytes.length);
      _free(buffer);
    };

    socket.onopen = function() {
      entry.error = "";
      {{{ makeDynCall('vi', 'openCallback') }}}(instance);
    };

    socket.onmessage = function(event) {
      if (event.data instanceof ArrayBuffer) {
        invokeRecv(event.data);
        return;
      }

      if (typeof Blob !== "undefined" && event.data instanceof Blob) {
        var reader = new FileReader();
        reader.onload = function() {
          invokeRecv(reader.result);
        };
        reader.onerror = function() {
          entry.error = "Failed to read websocket blob payload.";
          {{{ makeDynCall('vii', 'errorCallback') }}}(instance, 1006);
        };
        reader.readAsArrayBuffer(event.data);
      }
    };

    socket.onerror = function() {
      if (!entry.error) {
        entry.error = "WebSocket error.";
      }
      {{{ makeDynCall('vii', 'errorCallback') }}}(instance, 1006);
    };

    socket.onclose = function(event) {
      entry.closeCode = event && typeof event.code === "number" ? event.code : 1005;
      if (!entry.error && entry.closeCode !== 1000) {
        entry.error = event && event.reason ? event.reason : "WebSocket closed unexpectedly.";
      }
      {{{ makeDynCall('vii', 'closeCallback') }}}(instance, entry.closeCode);
    };

    Module.PHOTON_WebSocketInstances.push(entry);
    return instance;
  },

  SocketState: function(socketInstance) {
    var instances = Module.PHOTON_WebSocketInstances || [];
    var entry = instances[socketInstance];
    if (!entry || !entry.socket) {
      return 0;
    }
    return entry.socket.readyState === 1 ? 1 : 0;
  },

  SocketSend: function(socketInstance, ptr, length) {
    var entry = (Module.PHOTON_WebSocketInstances || [])[socketInstance];
    if (!entry || !entry.socket || entry.socket.readyState !== 1) {
      return;
    }

    var payload = HEAPU8.subarray(ptr, ptr + length);
    entry.socket.send(payload);
  },

  SocketClose: function(socketInstance) {
    var entry = (Module.PHOTON_WebSocketInstances || [])[socketInstance];
    if (!entry || !entry.socket) {
      return;
    }
    entry.socket.close();
  },

  SocketError: function(socketInstance, ptr, length) {
    var entry = (Module.PHOTON_WebSocketInstances || [])[socketInstance];
    if (!entry || !entry.error) {
      return 0;
    }

    stringToUTF8(entry.error, ptr, length);
    return 1;
  }
});
