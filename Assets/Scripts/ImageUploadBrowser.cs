using System.Collections;
using System.IO;
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

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;

        Debug.Log(message);
    }
}