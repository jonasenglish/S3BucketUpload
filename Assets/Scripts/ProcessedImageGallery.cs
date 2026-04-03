using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ProcessedImageGallery : MonoBehaviour
{
    [Header("Endpoints")]
    [SerializeField] private string listProcessedImagesEndpoint;
    [SerializeField] private string deleteProcessedImageEndpoint;

    [Header("UI")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GalleryImageTile tilePrefab;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button deleteButton;

    [Serializable]
    public class ImageItem
    {
        public string key;
        public string downloadUrl;
        public string sha256;
        public string lastModified;
        public long size;
    }

    [Serializable]
    public class ImageListResponse
    {
        public List<ImageItem> items;
    }

    [Serializable]
    private class DeleteRequest
    {
        public string key;
        public string sha256;
    }

    private readonly List<Texture2D> loadedTextures = new();
    private ImageItem selectedItem;

    private void Awake()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshGallery);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(DeleteSelectedImage);

        SetDeleteInteractable(false);
    }

    public void RefreshGallery()
    {
        StartCoroutine(LoadGallery());
    }

    public void DeleteSelectedImage()
    {
        if (selectedItem == null)
        {
            SetStatus("No image selected.");
            return;
        }

        StartCoroutine(DeleteImageCoroutine(selectedItem));
    }

    private IEnumerator LoadGallery()
    {
        SetStatus("Loading images...");
        ClearExistingImages();
        selectedItem = null;
        SetDeleteInteractable(false);

        using UnityWebRequest request = UnityWebRequest.Get(listProcessedImagesEndpoint);
        request.downloadHandler = new DownloadHandlerBuffer();
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
        {
            SetStatus($"Failed to load list: {request.responseCode} {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        ImageListResponse response;
        try
        {
            response = JsonUtility.FromJson<ImageListResponse>(request.downloadHandler.text);
        }
        catch (Exception ex)
        {
            SetStatus($"Parse error: {ex.Message}");
            yield break;
        }

        if (response?.items == null || response.items.Count == 0)
        {
            SetStatus("No processed images found.");
            yield break;
        }

        foreach (var item in response.items)
        {
            yield return StartCoroutine(DownloadAndDisplayImage(item));
        }

        SetStatus("Gallery loaded.");
    }

    private IEnumerator DownloadAndDisplayImage(ImageItem item)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(item.downloadUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Failed to download {item.key}: {request.responseCode} {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        loadedTextures.Add(texture);

        GalleryImageTile tile = Instantiate(tilePrefab, contentParent);
        tile.gameObject.SetActive(true);
        tile.Setup(texture, item, OnTileSelected);
    }

    private void OnTileSelected(ImageItem item, GalleryImageTile tile)
    {
        selectedItem = item;
        SetDeleteInteractable(true);
        SetStatus($"Selected: {item.key}");
    }

    private IEnumerator DeleteImageCoroutine(ImageItem item)
    {
        SetStatus($"Deleting {item.key}...");

        var body = new DeleteRequest
        {
            key = item.key,
            sha256 = item.sha256
        };

        string json = JsonUtility.ToJson(body);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(deleteProcessedImageEndpoint, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(jsonBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
        {
            SetStatus($"Delete failed: {request.responseCode} {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        SetStatus($"Deleted: {item.key}");
        RefreshGallery();
    }

    private void ClearExistingImages()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        foreach (var tex in loadedTextures)
            if (tex != null) Destroy(tex);

        loadedTextures.Clear();
    }

    private void SetDeleteInteractable(bool value)
    {
        if (deleteButton != null)
            deleteButton.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;

        Debug.Log(message);
    }
}