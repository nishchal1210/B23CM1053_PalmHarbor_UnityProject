#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Optional, explicitly started scenic frame capture for the assignment video.</summary>
public sealed class PalmHarborVideoCapture : MonoBehaviour
{
    public Camera captureCamera;
    public PalmHarborExplorer explorer;
    public int width = 1280;
    public int height = 720;
    public int frameRate = 20;
    public float duration = 40;
    public int jpegQuality = 90;

    public float Progress { get; private set; }
    public bool Finished { get; private set; }
    public bool IsRecording { get; private set; }
    public int FramesWritten { get; private set; }
    public string OutputDirectory { get; private set; }
    public string Error { get; private set; }

    RenderTexture renderTarget, savedTarget;
    Texture2D readback;
    Canvas[] canvases;
    bool[] canvasStates;
    bool explorerWasEnabled, previousBackground, cameraWasEnabled, prepared;
    Vector3 savedPosition;
    Quaternion savedRotation;
    float savedFov, savedAspect;
    int savedCaptureFramerate;
    CursorLockMode savedCursorLock;
    bool savedCursorVisible;
    Coroutine recording;

    struct Shot
    {
        public Vector3 start, end, targetStart, targetEnd;
        public float fov;
        public Shot(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float f)
        { start = a; end = b; targetStart = c; targetEnd = d; fov = f; }
    }

    // Each shot is deterministic and stays outside the facades and above the pavement.
    readonly Shot[] shots = {
        new Shot(new Vector3(12,4,-72), new Vector3(12,4,-35), new Vector3(-28,7,-42), new Vector3(-28,7,-10), 63),
        new Shot(new Vector3(0,4,25), new Vector3(-6,5,50), new Vector3(-33,10,35), new Vector3(-33,10,44), 59),
        new Shot(new Vector3(43,3,-35), new Vector3(47,4,17), new Vector3(80,1,-9), new Vector3(80,1,38), 66),
        new Shot(new Vector3(72,6,36), new Vector3(52,6,52), new Vector3(60,3,28), new Vector3(60,3,28), 60),
        new Shot(new Vector3(55,25,77), new Vector3(48,22,-22), new Vector3(-15,4,22), new Vector3(-15,4,-10), 54),
        new Shot(new Vector3(4,4,6), new Vector3(-14,2.8f,7), new Vector3(-34,3,5), new Vector3(-34,3,5), 58)
    };

    /// <summary>Start explicitly during Play mode. Writes frame_00000.jpg onward.</summary>
    public void Begin()
    {
        if (!Application.isPlaying || IsRecording) return;
        if (!captureCamera) captureCamera = GetComponent<Camera>();
        if (!captureCamera) captureCamera = Camera.main;
        if (!captureCamera) { Error = "No capture camera found."; Debug.LogError(Error); return; }
        if (!explorer) explorer = FindFirstObjectByType<PalmHarborExplorer>();
        width = Mathf.Max(64, width);
        height = Mathf.Max(64, height);
        frameRate = Mathf.Clamp(frameRate, 1, 60);
        duration = Mathf.Max(1, duration);
        OutputDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Submission", "VideoFrames");
        Progress = 0;
        Finished = false;
        FramesWritten = 0;
        Error = null;
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            SaveState();
            renderTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = "Palm Harbor Submission Capture", antiAliasing = 1 };
            renderTarget.Create();
            readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            if (explorer) explorer.enabled = false;
            foreach (Canvas canvas in canvases) if (canvas) canvas.enabled = false;
            Application.runInBackground = true;
            Time.captureFramerate = frameRate;
            // Manual offscreen rendering continues even when the Game view is unfocused.
            captureCamera.enabled = false;
            captureCamera.targetTexture = renderTarget;
            captureCamera.aspect = (float)width / height;
            IsRecording = true;
            recording = StartCoroutine(RecordFrames());
            Debug.Log("Palm Harbor video frames: " + OutputDirectory);
        }
        catch (Exception exception)
        {
            Error = exception.ToString();
            Debug.LogException(exception);
            RestoreState();
        }
    }

    void SaveState()
    {
        savedPosition = captureCamera.transform.position;
        savedRotation = captureCamera.transform.rotation;
        savedFov = captureCamera.fieldOfView;
        savedAspect = captureCamera.aspect;
        savedTarget = captureCamera.targetTexture;
        cameraWasEnabled = captureCamera.enabled;
        previousBackground = Application.runInBackground;
        savedCaptureFramerate = Time.captureFramerate;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        explorerWasEnabled = explorer && explorer.enabled;
        canvases = explorer ? explorer.GetComponentsInChildren<Canvas>(true) : Array.Empty<Canvas>();
        canvasStates = new bool[canvases.Length];
        for (int i = 0; i < canvases.Length; i++) canvasStates[i] = canvases[i].enabled;
        prepared = true;
    }

    IEnumerator RecordFrames()
    {
        int frameCount = Mathf.RoundToInt(duration * frameRate);
        var renderRequest = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTarget };
        var endOfFrame = new WaitForEndOfFrame();
        for (int frame = 0; frame < frameCount; frame++)
        {
            yield return endOfFrame;
            bool successful = true;
            try
            {
                float sequence = frame / (float)frameCount * shots.Length;
                int shotIndex = Mathf.Min(Mathf.FloorToInt(sequence), shots.Length - 1);
                float t = Mathf.SmoothStep(0, 1, sequence - shotIndex);
                Shot shot = shots[shotIndex];
                Vector3 position = Vector3.Lerp(shot.start, shot.end, t);
                Vector3 target = Vector3.Lerp(shot.targetStart, shot.targetEnd, t);
                captureCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                captureCamera.fieldOfView = shot.fov;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    if (GraphicsSettings.currentRenderPipeline)
                        RenderPipeline.SubmitRenderRequest(captureCamera, renderRequest);
                    else captureCamera.Render();
                    RenderTexture.active = renderTarget;
                    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    readback.Apply(false, false);
                    byte[] jpeg = readback.EncodeToJPG(Mathf.Clamp(jpegQuality, 1, 100));
                    File.WriteAllBytes(Path.Combine(OutputDirectory, "frame_" + frame.ToString("D5") + ".jpg"), jpeg);
                }
                finally { RenderTexture.active = previous; }
                FramesWritten = frame + 1;
                Progress = FramesWritten / (float)frameCount;
            }
            catch (Exception exception)
            {
                Error = exception.ToString();
                Debug.LogException(exception);
                successful = false;
            }
            if (!successful) break;
        }
        Finished = FramesWritten == frameCount;
        RestoreState();
        recording = null;
        Debug.Log(Finished ? "Palm Harbor cinematic complete: " + FramesWritten + " JPEG frames at " + frameRate + " fps." : "Palm Harbor cinematic stopped: " + Error);
    }

    public void Stop()
    {
        if (recording != null) { StopCoroutine(recording); recording = null; }
        RestoreState();
    }

    void RestoreState()
    {
        IsRecording = false;
        if (prepared)
        {
            if (captureCamera)
            {
                captureCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                captureCamera.fieldOfView = savedFov;
                captureCamera.aspect = savedAspect;
                captureCamera.targetTexture = savedTarget;
                captureCamera.enabled = cameraWasEnabled;
            }
            if (explorer) explorer.enabled = explorerWasEnabled;
            for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].enabled = canvasStates[i];
            Application.runInBackground = previousBackground;
            Time.captureFramerate = savedCaptureFramerate;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
            prepared = false;
        }
        if (renderTarget) { renderTarget.Release(); Destroy(renderTarget); renderTarget = null; }
        if (readback) { Destroy(readback); readback = null; }
    }

    void OnDisable() { Stop(); }
    void OnDestroy() { Stop(); }
}
#endif
