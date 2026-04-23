using System.Collections;
using TMPro;
using UnityEngine;

public class WelcomeTextController : MonoBehaviour
{
    [SerializeField] private TMP_Text welcomeLabel;

    private IEnumerator Start()
    {
        yield return RuntimeConfigLoader.WaitUntilLoaded();
        Refresh();
    }

    public void Refresh()
    {
        if (welcomeLabel == null)
        {
            Debug.LogWarning("[WelcomeTextController] Welcome label is not assigned.");
            return;
        }

        welcomeLabel.text = RuntimeConfigLoader.Instance?.WelcomeMessage ?? "Welcome";
    }
}