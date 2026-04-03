using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GalleryImageTile : MonoBehaviour
{
    [SerializeField] private RawImage imageView;
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text label;

    private ProcessedImageGallery.ImageItem item;
    private Action<ProcessedImageGallery.ImageItem, GalleryImageTile> onSelected;

    public void Setup(
        Texture2D texture,
        ProcessedImageGallery.ImageItem imageItem,
        Action<ProcessedImageGallery.ImageItem, GalleryImageTile> selectedCallback)
    {
        item = imageItem;
        onSelected = selectedCallback;

        imageView.texture = texture;
        if (label != null)
            label.text = imageItem.key;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(item, this));
    }
}
