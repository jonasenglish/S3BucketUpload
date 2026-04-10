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
    private static extern void OpenImageFilePicker(string gameObjectName, string callbackMethod);

    [Serializable]
    private class PickedFilePayload
    {
        public string fileName;
        public string contentType;
        public string base64;
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
        OpenImageFilePicker(gameObject.name, nameof(OnWebGLFilePicked));
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

        StartCoroutine(UploadSelectedFile(filePath));
    }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
    public void OnWebGLFilePicked(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            SetStatus("No file selected.");
            return;
        }

        PickedFilePayload payload;
        try
        {
            payload = JsonUtility.FromJson<PickedFilePayload>(json);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to parse selected file: {ex.Message}");
            return;
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.fileName) || string.IsNullOrWhiteSpace(payload.base64))
        {
            SetStatus("Selected file data was invalid.");
            return;
        }

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(payload.base64);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to decode selected file: {ex.Message}");
            return;
        }

        StartCoroutine(UploadSelectedFile(payload.fileName, payload.contentType, fileBytes));
    }
#endif

    private IEnumerator UploadSelectedFile(string filePath)
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

    private IEnumerator UploadSelectedFile(string fileName, string contentType, byte[] fileBytes)
    {
        isUploading = true;
        SetStatus($"Preparing {fileName}...");

        AwsImageUploadService.PresignResponse response = null;
        string error = null;

        yield return uploadService.UploadImageBytes(
            fileBytes,
            fileName,
            contentType,
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

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;

        Debug.Log(message);
    }
}