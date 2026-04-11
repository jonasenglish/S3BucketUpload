mergeInto(LibraryManager.library, {
  OpenImageFilePicker_MetadataOnly: function (
    gameObjectNamePtr,
    callbackMethodPtr,
  ) {
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

    input.onchange = function () {
      const file = input.files && input.files[0];
      if (!file) {
        SendMessage(gameObjectName, callbackMethod, "");
        return;
      }

      window.__unitySelectedUploadFile = file;

      const payload = JSON.stringify({
        fileName: file.name || "",
        contentType: file.type || "",
        fileSize: file.size || 0,
      });

      SendMessage(gameObjectName, callbackMethod, payload);
      input.value = "";
    };

    input.click();
  },

  UploadSelectedBrowserFile: function (
    gameObjectNamePtr,
    callbackMethodPtr,
    uploadUrlPtr,
    contentTypePtr,
  ) {
    const gameObjectName = UTF8ToString(gameObjectNamePtr);
    const callbackMethod = UTF8ToString(callbackMethodPtr);
    const uploadUrl = UTF8ToString(uploadUrlPtr);
    const contentType = UTF8ToString(contentTypePtr);

    const file = window.__unitySelectedUploadFile;
    if (!file) {
      SendMessage(
        gameObjectName,
        callbackMethod,
        JSON.stringify({
          success: false,
          error: "No browser file is currently selected.",
        }),
      );
      return;
    }

    fetch(uploadUrl, {
      method: "PUT",
      headers: {
        "Content-Type": contentType,
      },
      body: file,
    })
      .then(async (response) => {
        if (!response.ok) {
          let text = "";
          try {
            text = await response.text();
          } catch (_) {}

          SendMessage(
            gameObjectName,
            callbackMethod,
            JSON.stringify({
              success: false,
              error:
                "S3 upload failed with status " +
                response.status +
                (text ? ": " + text : ""),
            }),
          );
          return;
        }

        window.__unitySelectedUploadFile = null;

        SendMessage(
          gameObjectName,
          callbackMethod,
          JSON.stringify({
            success: true,
            error: "",
          }),
        );
      })
      .catch((err) => {
        SendMessage(
          gameObjectName,
          callbackMethod,
          JSON.stringify({
            success: false,
            error: err && err.message ? err.message : String(err),
          }),
        );
      });
  },
});
