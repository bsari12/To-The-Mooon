using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraLetterbox : MonoBehaviour
{
    // hedef referans en-boy oranı: örn 9/16 = 0.5625
    public float targetAspect = 9f / 16f;

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1.0f)
        {
            // siyah üst/alt çubuk (letterbox)
            Rect rect = cam.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            cam.rect = rect;
        }
        else
        {
            // siyah yan çubuk (pillarbox)
            float scaleWidth = 1.0f / scaleHeight;
            Rect rect = cam.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            cam.rect = rect;
        }
    }
}
