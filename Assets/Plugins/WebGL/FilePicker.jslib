mergeInto(LibraryManager.library, {
  OpenImageFilePicker: function (gameObjectNamePtr, callbackMethodPtr) {
    const gameObjectName = UTF8ToString(gameObjectNamePtr);
    const callbackMethod = UTF8ToString(callbackMethodPtr);

    let input = document.getElementById("unity-image-file-input");
    if (!input) {
      input = document.createElement("input");
      input.type = "file";
      input.accept =
        ".png,.jpg,.jpeg,.webp,.gif,image/png,image/jpeg,image/webp,image/gif";
      input.id = "unity-image-file-input";
      input.style.display = "none";
      document.body.appendChild(input);
    }

    input.onchange = async function () {
      const file = input.files && input.files[0];
      if (!file) {
        SendMessage(gameObjectName, callbackMethod, "");
        return;
      }

      const arrayBuffer = await file.arrayBuffer();
      const bytes = new Uint8Array(arrayBuffer);

      let binary = "";
      const chunkSize = 0x8000;
      for (let i = 0; i < bytes.length; i += chunkSize) {
        binary += String.fromCharCode.apply(
          null,
          bytes.subarray(i, i + chunkSize),
        );
      }

      const payload = JSON.stringify({
        fileName: file.name,
        contentType: file.type || "",
        base64: btoa(binary),
      });

      SendMessage(gameObjectName, callbackMethod, payload);
      input.value = "";
    };

    input.click();
  },
});
