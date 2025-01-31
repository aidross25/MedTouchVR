using UnityEngine;
using TMPro;  // Required for TextMeshPro

public class timeIndicator : MonoBehaviour
{
    public float elapsedTime = 0f;
    public bool isRunning = true;
    public TextMeshProUGUI timerText;  // Drag & drop a TextMeshProUGUI element

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;

            if (timerText != null)
                timerText.text = "Time: " + elapsedTime.ToString("F2");  // Format to 2 decimal places
        }
    }
}
