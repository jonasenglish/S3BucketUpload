using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SFB;

public class ImageUploadBrowser : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AwsImageUploadService uploadService;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button browseButton;

    private bool isUploading;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OpenImageFilePicker_MetadataOnly(string gameObjectName, string callbackMethod);

    [DllImport("__Internal")]
    private static extern void UploadSelectedBrowserFile(string gameObjectName, string callbackMethod, string uploadUrl, string contentType);

    [Serializable]
    private class PickedFileMetadata
    {
        public string fileName;
        public string contentType;
        public int fileSize;
    }

    [Serializable]
    private class BrowserUploadResult
    {
        public bool success;
        public string error;
    }
#endif

    private void Awake()
    {
        SetStatus("Select an image to upload.");

        if (browseButton != null)
            browseButton.onClick.AddListener(BrowseForImage);
    }

    private void OnDestroy()
    {
        if (browseButton != null)
            browseButton.onClick.RemoveListener(BrowseForImage);
    }

    public void BrowseForImage()
    {
        if (isUploading)
        {
            SetStatus("Upload already in progress.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        SetStatus("Opening browser file picker...");
        OpenImageFilePicker_MetadataOnly(gameObject.name, nameof(OnWebGLFileSelected));
#else
        BrowseForImageDesktop();
#endif
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private void BrowseForImageDesktop()
    {
        var extensions = new[]
        {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg", "webp", "gif"),
            new ExtensionFilter("PNG", "png"),
            new ExtensionFilter("JPEG", "jpg", "jpeg"),
            new ExtensionFilter("WebP", "webp"),
            new ExtensionFilter("GIF", "gif")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Image",
            "",
            extensions,
            false);

        if (paths == null || paths.Length == 0 || string.IsNullOrWhiteSpace(paths[0]))
        {
            SetStatus("No file selected.");
            return;
        }

        string filePath = paths[0];

        if (!File.Exists(filePath))
        {
            SetStatus("Selected file was not found.");
            return;
        }

        StartCoroutine(UploadSelectedFileDesktop(filePath));
    }
#endif

    private IEnumerator UploadSelectedFileDesktop(string filePath)
    {
        isUploading = true;
        SetStatus($"Preparing {Path.GetFileName(filePath)}...");

        AwsImageUploadService.PresignResponse response = null;
        string error = null;

        yield return uploadService.UploadImageFile(
            filePath,
            successResponse => response = successResponse,
            errorMessage => error = errorMessage);

        isUploading = false;

        if (!string.IsNullOrEmpty(error))
        {
            SetStatus($"Upload failed:\n{error}");
            yield break;
        }

        SetStatus($"Upload complete!\n{response.objectKey}");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public void OnWebGLFileSelected(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("No file selected.");
            return;
        }

        PickedFileMetadata metadata;
        try
        {
            metadata = JsonUtility.FromJson<PickedFileMetadata>(json);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to parse selected file metadata: {ex.Message}");
            return;
        }

        if (metadata == null || string.IsNullOrWhiteSpace(metadata.fileName) || string.IsNullOrWhiteSpace(metadata.contentType))
        {
            SetStatus("Selected file metadata was invalid.");
            return;
        }

        if (metadata.fileSize <= 0)
        {
            SetStatus("File was empty.");
            return;
        }

        if (metadata.fileSize > uploadService.MaxFileSizeBytes)
        {
            SetStatus($"File exceeds max size of {uploadService.MaxFileSizeBytes} bytes.");
            return;
        }

        StartCoroutine(RequestPresignAndUploadWebGL(metadata));
    }

    private IEnumerator RequestPresignAndUploadWebGL(PickedFileMetadata metadata)
    {
        isUploading = true;
        SetStatus($"Preparing {metadata.fileName}...");

        AwsImageUploadService.PresignResponse response = null;
        string error = null;

        yield return uploadService.RequestPresignedUrlForBrowser(
            metadata.fileName,
            metadata.contentType,
            successResponse => response = successResponse,
            errorMessage => error = errorMessage);

        if (!string.IsNullOrEmpty(error) || response == null)
        {
            isUploading = false;
            SetStatus($"Upload failed:\n{error ?? "Failed to get presigned URL."}");
            yield break;
        }

        SetStatus("Uploading file...");
        UploadSelectedBrowserFile(gameObject.name, nameof(OnWebGLUploadFinished), response.uploadUrl, response.contentType);
    }

    public void OnWebGLUploadFinished(string json)
    {
        isUploading = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("Upload failed:\nNo response from browser upload.");
            return;
        }

        BrowserUploadResult result;
        try
        {
            result = JsonUtility.FromJson<BrowserUploadResult>(json);
        }
        catch (Exception ex)
        {
            SetStatus($"Upload failed:\nCould not parse browser response: {ex.Message}");
            return;
        }

        if (result == null)
        {
            SetStatus("Upload failed:\nInvalid browser response.");
            return;
        }

        if (!result.success)
        {
            SetStatus($"Upload failed:\n{result.error}");
            return;
        }

        SetStatus("Upload complete!");
    }
#endif

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;

        Debug.Log(message);
    }
}