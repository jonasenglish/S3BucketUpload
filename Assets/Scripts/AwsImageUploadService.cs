using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AwsImageUploadService : MonoBehaviour
{
    [Header("Presign API")]
    [SerializeField] private string presignEndpoint;

    [Header("Upload Settings")]
    [SerializeField] private int maxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [Serializable]
    private class PresignRequest
    {
        public string fileName;
        public string contentType;
    }

    [Serializable]
    public class PresignResponse
    {
        public string uploadUrl;
        public string bucket;
        public string objectKey;
        public string contentType;
        public int expiresIn;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string error;
        public string details;
    }

    public IEnumerator UploadImageFile(
        string filePath,
        Action<PresignResponse> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            onError?.Invoke("File path was empty.");
            yield break;
        }

        if (!File.Exists(filePath))
        {
            onError?.Invoke($"File does not exist: {filePath}");
            yield break;
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string contentType = GetContentType(extension);

        if (string.IsNullOrEmpty(contentType))
        {
            onError?.Invoke($"Unsupported image type: {extension}");
            yield break;
        }

        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Failed to read file: {ex.Message}");
            yield break;
        }

        if (fileBytes.Length == 0)
        {
            onError?.Invoke("File was empty.");
            yield break;
        }

        if (fileBytes.Length > maxFileSizeBytes)
        {
            onError?.Invoke($"File exceeds max size of {maxFileSizeBytes} bytes.");
            yield break;
        }

        string fileName = Path.GetFileName(filePath);

        PresignResponse presignResponse = null;
        bool presignComplete = false;
        string presignError = null;

        yield return RequestPresignedUrl(
            fileName,
            contentType,
            response =>
            {
                presignResponse = response;
                presignComplete = true;
            },
            error =>
            {
                presignError = error;
                presignComplete = true;
            });

        if (!presignComplete || presignResponse == null)
        {
            onError?.Invoke($"Presign failed: {presignError ?? "Unknown error"}");
            yield break;
        }

        bool uploadComplete = false;
        string uploadError = null;

        yield return UploadToPresignedUrl(
            presignResponse.uploadUrl,
            fileBytes,
            contentType,
            () => uploadComplete = true,
            error =>
            {
                uploadError = error;
                uploadComplete = true;
            });

        if (!uploadComplete)
        {
            onError?.Invoke("Upload did not complete.");
            yield break;
        }

        if (!string.IsNullOrEmpty(uploadError))
        {
            onError?.Invoke(uploadError);
            yield break;
        }

        onSuccess?.Invoke(presignResponse);
    }

    private IEnumerator RequestPresignedUrl(
        string fileName,
        string contentType,
        Action<PresignResponse> onSuccess,
        Action<string> onError)
    {
        PresignRequest requestBody = new PresignRequest
        {
            fileName = fileName,
            contentType = contentType
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        using UnityWebRequest request = new UnityWebRequest(presignEndpoint, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(jsonBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"HTTP error while presigning: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        if (request.responseCode < 200 || request.responseCode >= 300)
        {
            onError?.Invoke($"Presign request failed with status {request.responseCode}: {request.downloadHandler.text}");
            yield break;
        }

        try
        {
            PresignResponse response = JsonUtility.FromJson<PresignResponse>(request.downloadHandler.text);

            if (response == null || string.IsNullOrWhiteSpace(response.uploadUrl))
            {
                onError?.Invoke($"Presign response was invalid: {request.downloadHandler.text}");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Failed to parse presign response: {ex.Message}\nRaw: {request.downloadHandler.text}");
        }
    }

    private IEnumerator UploadToPresignedUrl(
        string uploadUrl,
        byte[] fileBytes,
        string contentType,
        Action onSuccess,
        Action<string> onError)
    {
        using UnityWebRequest request = new UnityWebRequest(uploadUrl, UnityWebRequest.kHttpVerbPUT);
        request.uploadHandler = new UploadHandlerRaw(fileBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", contentType);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"S3 upload failed: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        if (request.responseCode < 200 || request.responseCode >= 300)
        {
            onError?.Invoke($"S3 upload returned status {request.responseCode}: {request.downloadHandler.text}");
            yield break;
        }

        onSuccess?.Invoke();
    }

    private static string GetContentType(string extension)
    {
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null
        };
    }
}
