using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class RoomFrostedScreenBlur : MonoBehaviour
{
    [Header("Blur")]
    public Shader blurShader;

    [Range(0, 4)]
    public int downsample = 2;

    [Range(0, 6)]
    public int iterations = 2;

    [Range(0f, 6f)]
    public float blurSize = 1.5f;

    private Material blurMaterial;

    private void OnEnable()
    {
        EnsureMaterial();
    }

    private void OnDisable()
    {
        CleanupMaterial();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!EnsureMaterial() || iterations <= 0 || blurSize <= 0f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        int width = Mathf.Max(1, source.width >> downsample);
        int height = Mathf.Max(1, source.height >> downsample);

        RenderTexture currentSource = source;
        RenderTexture currentDestination = RenderTexture.GetTemporary(width, height, 0, source.format);
        currentDestination.filterMode = FilterMode.Bilinear;

        blurMaterial.SetFloat("_BlurSize", blurSize);
        Graphics.Blit(currentSource, currentDestination, blurMaterial);

        currentSource = currentDestination;

        for (int i = 1; i < iterations; i++)
        {
            currentDestination = RenderTexture.GetTemporary(width, height, 0, source.format);
            currentDestination.filterMode = FilterMode.Bilinear;

            blurMaterial.SetFloat("_BlurSize", blurSize + i);
            Graphics.Blit(currentSource, currentDestination, blurMaterial);

            RenderTexture.ReleaseTemporary(currentSource);
            currentSource = currentDestination;
        }

        Graphics.Blit(currentSource, destination);
        RenderTexture.ReleaseTemporary(currentSource);
    }

    private bool EnsureMaterial()
    {
        if (blurShader == null)
        {
            blurShader = Shader.Find("Hidden/Room/FrostedScreenBlur");
        }

        if (blurShader == null || !blurShader.isSupported)
        {
            return false;
        }

        if (blurMaterial == null)
        {
            blurMaterial = new Material(blurShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return true;
    }

    private void CleanupMaterial()
    {
        if (blurMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(blurMaterial);
        }
        else
        {
            DestroyImmediate(blurMaterial);
        }

        blurMaterial = null;
    }
}
