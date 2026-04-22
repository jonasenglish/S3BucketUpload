using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class RuntimeConfig
{
    public string uploadUrl;
    public string viewImagesUrl;
    public string deleteImageUrlTemplate;
    public string hostedUiLoginUrl;
}

[DefaultExecutionOrder(-1000)]
public class RuntimeConfigLoader : MonoBehaviour
{
    private const string AccessTokenKey = "cognito.access_token";
    private const string IdTokenKey = "cognito.id_token";

    public static RuntimeConfigLoader Instance { get; private set; }

    public RuntimeConfig Config { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool LoadFailed { get; private set; }

    public string AccessToken { get; private set; }
    public string IdToken { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(IdToken) ||
        !string.IsNullOrWhiteSpace(AccessToken);

    public static string UploadUrl => Instance?.Config?.uploadUrl;
    public static string ViewImagesUrl => Instance?.Config?.viewImagesUrl;
    public static string DeleteImageUrlTemplate => Instance?.Config?.deleteImageUrlTemplate;

    private void Awake()
    {
        Debug.Log("[RuntimeConfigLoader] Awake.");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RuntimeConfigLoader] Duplicate instance detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[RuntimeConfigLoader] Starting runtime config load.");
        StartCoroutine(LoadConfigCoroutine());
    }

    private IEnumerator LoadConfigCoroutine()
    {
        const string runtimeConfigPath = "runtime-config.json";
        Debug.Log($"[RuntimeConfigLoader] Requesting runtime config from '{runtimeConfigPath}'.");

        using var req = UnityWebRequest.Get(runtimeConfigPath);
        yield return req.SendWebRequest();

        Debug.Log(
            $"[RuntimeConfigLoader] Runtime config request completed. " +
            $"Result={req.result}, StatusCode={req.responseCode}, Error='{req.error}'.");

        if (req.result != UnityWebRequest.Result.Success)
        {
            LoadFailed = true;
            Debug.LogError($"[RuntimeConfigLoader] Failed to load runtime config: {req.error}");
            yield break;
        }

        Config = JsonUtility.FromJson<RuntimeConfig>(req.downloadHandler.text);
        if (Config == null)
        {
            LoadFailed = true;
            Debug.LogError("[RuntimeConfigLoader] Runtime config JSON could not be parsed.");
            yield break;
        }

        Debug.Log(
            $"[RuntimeConfigLoader] Config loaded. " +
            $"uploadUrl='{Config.uploadUrl}', viewImagesUrl='{Config.viewImagesUrl}', deleteImageUrlTemplate='{Config.deleteImageUrlTemplate}'.");

        Debug.Log(
            $"[RuntimeConfigLoader] Application.absoluteURL present={ !string.IsNullOrWhiteSpace(Application.absoluteURL) }, " +
            $"containsFragment={Application.absoluteURL?.Contains('#') == true}.");

        AccessToken = TryGetFragmentValue(Application.absoluteURL, "access_token");
        IdToken = TryGetFragmentValue(Application.absoluteURL, "id_token");

        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            PlayerPrefs.SetString(AccessTokenKey, AccessToken);
            Debug.Log($"[RuntimeConfigLoader] Access token found in URL fragment. {DescribeToken(AccessToken)}");
        }
        else
        {
            AccessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
            Debug.Log(
                !string.IsNullOrWhiteSpace(AccessToken)
                    ? $"[RuntimeConfigLoader] Access token restored from PlayerPrefs. {DescribeToken(AccessToken)}"
                    : "[RuntimeConfigLoader] No access token found in URL fragment or PlayerPrefs.");
        }

        if (!string.IsNullOrWhiteSpace(IdToken))
        {
            PlayerPrefs.SetString(IdTokenKey, IdToken);
            Debug.Log($"[RuntimeConfigLoader] ID token found in URL fragment. {DescribeToken(IdToken)}");
        }
        else
        {
            IdToken = PlayerPrefs.GetString(IdTokenKey, string.Empty);
            Debug.Log(
                !string.IsNullOrWhiteSpace(IdToken)
                    ? $"[RuntimeConfigLoader] ID token restored from PlayerPrefs. {DescribeToken(IdToken)}"
                    : "[RuntimeConfigLoader] No ID token found in URL fragment or PlayerPrefs.");
        }

        PlayerPrefs.Save();

        IsLoaded = true;
        Debug.Log(
            $"[RuntimeConfigLoader] Load complete. " +
            $"IsAuthenticated={IsAuthenticated}, HasIdToken={!string.IsNullOrWhiteSpace(IdToken)}, HasAccessToken={!string.IsNullOrWhiteSpace(AccessToken)}");
    }

    public void ApplyAuth(UnityWebRequest request)
    {
        if (request == null)
        {
            Debug.LogWarning("[RuntimeConfigLoader] ApplyAuth called with null request.");
            return;
        }

        var usingIdToken = !string.IsNullOrWhiteSpace(IdToken);
        var token = usingIdToken ? IdToken : AccessToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            Debug.LogWarning(
                $"[RuntimeConfigLoader] No auth token available for request '{request.url}'. " +
                $"HasIdToken={ !string.IsNullOrWhiteSpace(IdToken) }, HasAccessToken={ !string.IsNullOrWhiteSpace(AccessToken) }");
            return;
        }

        request.SetRequestHeader("Authorization", $"Bearer {token}");

        Debug.Log(
            $"[RuntimeConfigLoader] Applied Authorization header to '{request.url}'. " +
            $"TokenType={(usingIdToken ? "id_token" : "access_token")}, {DescribeToken(token)}");
    }

    public void ClearAuth()
    {
        Debug.Log("[RuntimeConfigLoader] Clearing cached auth tokens.");

        AccessToken = null;
        IdToken = null;
        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.DeleteKey(IdTokenKey);
        PlayerPrefs.Save();
    }

    private static string TryGetFragmentValue(string url, string key)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning($"[RuntimeConfigLoader] Cannot extract '{key}' because URL is empty.");
            return null;
        }

        var hashIndex = url.IndexOf('#');
        if (hashIndex < 0 || hashIndex == url.Length - 1)
        {
            Debug.Log($"[RuntimeConfigLoader] URL fragment does not contain '{key}'.");
            return null;
        }

        var fragment = url[(hashIndex + 1)..];
        var parts = fragment.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in parts)
        {
            var kv = p.Split('=', 2);
            if (kv.Length == 2 && kv[0] == key)
            {
                var value = Uri.UnescapeDataString(kv[1]);
                Debug.Log($"[RuntimeConfigLoader] Extracted '{key}' from URL fragment. {DescribeToken(value)}");
                return value;
            }
        }

        Debug.Log($"[RuntimeConfigLoader] URL fragment did not include '{key}'.");
        return null;
    }

    private static string DescribeToken(string token)
    {
        return string.IsNullOrWhiteSpace(token)
            ? "token=missing"
            : $"tokenLength={token.Length}";
    }

    public static IEnumerator WaitUntilLoaded()
    {
        Debug.Log("[RuntimeConfigLoader] Waiting for runtime config to finish loading.");

        while (Instance == null || (!Instance.IsLoaded && !Instance.LoadFailed))
            yield return null;

        Debug.Log(
            $"[RuntimeConfigLoader] Wait complete. " +
            $"InstanceExists={Instance != null}, IsLoaded={Instance?.IsLoaded == true}, LoadFailed={Instance?.LoadFailed == true}");
    }
}