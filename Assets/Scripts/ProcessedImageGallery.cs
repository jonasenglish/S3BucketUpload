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
        yield return RuntimeConfigLoader.WaitUntilLoaded();

        if (RuntimeConfigLoader.Instance == null || RuntimeConfigLoader.Instance.LoadFailed)
        {
            SetStatus("Runtime config failed to load.");
            yield break;
        }

        SetStatus("Loading images...");
        ClearExistingImages();
        selectedItem = null;
        SetDeleteInteractable(false);

        Debug.Log("Retrieving images from: " + RuntimeConfigLoader.ViewImagesUrl);
        using UnityWebRequest request = UnityWebRequest.Get(RuntimeConfigLoader.ViewImagesUrl);
        request.downloadHandler = new DownloadHandlerBuffer();
        RuntimeConfigLoader.Instance.ApplyAuth(request);
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
            Debug.Log($"Received {response?.items?.Count ?? 0} items.");
            foreach (var item in response?.items ?? new List<ImageItem>())
            {
                Debug.Log($"Item: {item.key}, URL: {item.downloadUrl}, SHA256: {item.sha256}, LastModified: {item.lastModified}, Size: {item.size}");
            }
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
        yield return RuntimeConfigLoader.WaitUntilLoaded();

        if (RuntimeConfigLoader.Instance == null || RuntimeConfigLoader.Instance.LoadFailed)
        {
            SetStatus("Runtime config failed to load.");
            yield break;
        }

        SetStatus($"Deleting {item.key}...");

        string deleteUrl = RuntimeConfigLoader.DeleteImageUrlTemplate
            .Replace("{id+}", item.key)
            .Replace("{id}", item.key);

        Debug.Log($"Delete URL: {deleteUrl}");

        using UnityWebRequest request = UnityWebRequest.Delete(deleteUrl);
        request.downloadHandler = new DownloadHandlerBuffer();
        RuntimeConfigLoader.Instance.ApplyAuth(request);
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