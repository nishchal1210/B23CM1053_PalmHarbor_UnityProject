using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>First person exploration, original diner radio, photo mode and a presentation tour.</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PalmHarborExplorer : MonoBehaviour
{
    public Camera viewCamera;
    public Light sun;
    public Material skyMaterial;
    public Transform interactionPoint;
    public Light[] neonLights;
    public Renderer waterRenderer;
    public Material waterMaterial;

    public float DistanceTravelled { get; private set; }
    public int InteractionCount { get; private set; }
    public bool TourActive { get; private set; }
    public bool IsNight { get; private set; }
    public string LastPhotoPath { get; private set; }

    CharacterController controller;
    Canvas hud;
    UnityEngine.UI.Text prompt, status, notice, reticle;
    Font hudFont;
    AudioSource radio;
    AudioClip radioClip;
    Vector3 initialPosition, savedPosition, cameraHome;
    Quaternion initialRotation;
    float yaw, pitch, verticalSpeed, savedYaw, savedPitch, tourTime, noticeUntil;
    bool hudVisible = true, wantsCursor = true, captureBusy;
    float daySunIntensity, dayFogDensity, nightBlend;
    Color daySunColor, dayAmbientSky, dayAmbientEquator, dayAmbientGround, dayFog;
    float[] neonIntensities;
    Material originalSky, runtimeSky, originalWater, runtimeWater;
    readonly string[] skyColorNames = { "_TopColor", "_HorizonColor", "_BottomColor", "_SkyTint", "_GroundColor", "_SunColor" };
    readonly Color[] skyDayColors = new Color[6];
    readonly Color[] skyNightColors = {
        new Color(.018f,.025f,.10f), new Color(.22f,.09f,.25f), new Color(.035f,.04f,.09f),
        new Color(.12f,.13f,.25f), new Color(.05f,.045f,.08f), new Color(.04f,.05f,.085f)
    };
    readonly string[] waterColorNames = { "_DeepColor", "_ShallowColor", "_FoamColor", "_ReflectionColor", "_SunColor" };
    readonly Color[] waterDayColors = new Color[5];
    readonly Color[] waterNightColors = {
        new Color(.018f,.045f,.085f), new Color(.035f,.15f,.17f), new Color(.19f,.27f,.33f),
        new Color(.07f,.09f,.16f), new Color(.08f,.12f,.20f)
    };

    // Camera locations are deliberately above the pedestrian routes during the tour.
    readonly Vector3[] tourPositions = {
        new Vector3(12,6,-49), new Vector3(15,4,-24), new Vector3(16,4,8),
        new Vector3(31,5,33), new Vector3(43,11,43), new Vector3(7,21,55),
        new Vector3(-4,8,25), new Vector3(-7,3,-10)
    };
    readonly Vector3[] tourTargets = {
        new Vector3(-23,6,-22), new Vector3(-23,5,-15), new Vector3(-24,6,13),
        new Vector3(60,1,40), new Vector3(-10,5,15), new Vector3(-18,5,0),
        new Vector3(-25,5,8), new Vector3(-25,3,-15)
    };

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
        if (!viewCamera) { enabled = false; return; }
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        cameraHome = viewCamera.transform.localPosition;
        yaw = transform.eulerAngles.y;
        pitch = NormalizeAngle(viewCamera.transform.localEulerAngles.x);
        controller.stepOffset = .32f;
        controller.slopeLimit = 48;
        if (!viewCamera.GetComponent<AudioListener>()) viewCamera.gameObject.AddComponent<AudioListener>();
        RememberLighting();
        BuildHud();
        SetCursor(true);
    }

    void RememberLighting()
    {
        daySunIntensity = sun ? sun.intensity : 1.3f;
        daySunColor = sun ? sun.color : Color.white;
        dayAmbientSky = RenderSettings.ambientSkyColor;
        dayAmbientEquator = RenderSettings.ambientEquatorColor;
        dayAmbientGround = RenderSettings.ambientGroundColor;
        dayFog = RenderSettings.fogColor;
        dayFogDensity = RenderSettings.fogDensity;
        if (neonLights != null)
        {
            neonIntensities = new float[neonLights.Length];
            for (int i = 0; i < neonLights.Length; i++)
                if (neonLights[i]) neonIntensities[i] = neonLights[i].intensity;
        }
        originalSky = RenderSettings.skybox;
        if (skyMaterial)
        {
            runtimeSky = new Material(skyMaterial) { name = "Palm Harbor Runtime Sky" };
            skyMaterial = runtimeSky;
            RenderSettings.skybox = runtimeSky;
            for (int i = 0; i < skyColorNames.Length; i++)
                if (runtimeSky.HasProperty(skyColorNames[i])) skyDayColors[i] = runtimeSky.GetColor(skyColorNames[i]);
        }
        if (!waterRenderer)
        {
            var waterObject = GameObject.Find("Atlantic | animated coastal water");
            if (waterObject) waterRenderer = waterObject.GetComponent<Renderer>();
        }
        if (waterRenderer)
        {
            originalWater = waterRenderer.sharedMaterial;
            Material source = waterMaterial ? waterMaterial : originalWater;
            if (source)
            {
                runtimeWater = new Material(source) { name = "Palm Harbor Runtime Ocean" };
                waterMaterial = runtimeWater;
                waterRenderer.sharedMaterial = runtimeWater;
                for (int i = 0; i < waterColorNames.Length; i++)
                    if (runtimeWater.HasProperty(waterColorNames[i])) waterDayColors[i] = runtimeWater.GetColor(waterColorNames[i]);
            }
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) SetCursor(Cursor.lockState != CursorLockMode.Locked);
            if (keyboard.hKey.wasPressedThisFrame) { hudVisible = !hudVisible; hud.enabled = hudVisible; }
            if (keyboard.nKey.wasPressedThisFrame) ToggleNight();
            if (keyboard.fKey.wasPressedThisFrame) ToggleTour();
            if (keyboard.rKey.wasPressedThisFrame) ResetPlayer();
            if (keyboard.pKey.wasPressedThisFrame && !captureBusy) StartCoroutine(CapturePhoto());
            if (!TourActive && keyboard.eKey.wasPressedThisFrame && NearRadio()) ToggleRadio();
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursor(true);

        if (TourActive) UpdateTour();
        else if (Cursor.lockState == CursorLockMode.Locked) UpdateExplorer(keyboard, mouse);
        UpdateLighting();
        UpdateHud();
    }

    void UpdateExplorer(Keyboard keyboard, Mouse mouse)
    {
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * .085f;
            pitch = Mathf.Clamp(pitch - delta.y * .085f, -78, 78);
        }
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        if (keyboard == null) return;
        float horizontal = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
        float forward = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
        Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontal, 0, forward), 1);
        bool sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        Vector3 velocity = transform.TransformDirection(input) * (sprint ? 7.1f : 4.25f);
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame) verticalSpeed = 5.5f;
        verticalSpeed -= 19.6f * dt;
        velocity.y = verticalSpeed;
        Vector3 previous = transform.position;
        controller.Move(velocity * dt);
        Vector3 travelled = transform.position - previous;
        travelled.y = 0;
        DistanceTravelled += travelled.magnitude;
        if (transform.position.y < -12) ResetPlayer();
    }

    public void ToggleNight()
    {
        IsNight = !IsNight;
        ShowNotice(IsNight ? "After dark  /  neon coast" : "Golden hour  /  ocean breeze");
    }

    void UpdateLighting()
    {
        nightBlend = Mathf.MoveTowards(nightBlend, IsNight ? 1 : 0, Time.deltaTime * .45f);
        if (sun)
        {
            sun.intensity = Mathf.Lerp(daySunIntensity, .18f, nightBlend);
            sun.color = Color.Lerp(daySunColor, new Color(.47f,.6f,1), nightBlend);
        }
        RenderSettings.ambientSkyColor = Color.Lerp(dayAmbientSky, new Color(.12f,.15f,.28f), nightBlend);
        RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbientEquator, new Color(.15f,.13f,.22f), nightBlend);
        RenderSettings.ambientGroundColor = Color.Lerp(dayAmbientGround, new Color(.075f,.08f,.14f), nightBlend);
        RenderSettings.fogColor = Color.Lerp(dayFog, new Color(.09f,.10f,.19f), nightBlend);
        RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, .0028f, nightBlend);
        if (runtimeSky)
            for (int i = 0; i < skyColorNames.Length; i++)
                if (runtimeSky.HasProperty(skyColorNames[i]))
                    runtimeSky.SetColor(skyColorNames[i], Color.Lerp(skyDayColors[i], skyNightColors[i], nightBlend));
        if (runtimeWater)
            for (int i = 0; i < waterColorNames.Length; i++)
                if (runtimeWater.HasProperty(waterColorNames[i]))
                    runtimeWater.SetColor(waterColorNames[i], Color.Lerp(waterDayColors[i], waterNightColors[i], nightBlend));
        // Emissive signs remain visible at sunset; their surrounding glow grows at night.
        if (neonLights != null)
            for (int i = 0; i < neonLights.Length; i++)
                if (neonLights[i])
                {
                    neonLights[i].enabled = true;
                    if (neonIntensities != null && i < neonIntensities.Length)
                        neonLights[i].intensity = neonIntensities[i] * Mathf.Lerp(1, 1.45f, nightBlend);
                }
    }

    public void ToggleTour()
    {
        TourActive = !TourActive;
        if (TourActive)
        {
            savedPosition = transform.position;
            savedYaw = yaw;
            savedPitch = pitch;
            tourTime = 0;
            controller.enabled = false;
            ShowNotice("Cinematic tour  /  F to return to exploration");
        }
        else
        {
            transform.position = savedPosition;
            yaw = savedYaw;
            pitch = savedPitch;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            viewCamera.transform.localPosition = cameraHome;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            controller.enabled = true;
            verticalSpeed = 0;
        }
    }

    void UpdateTour()
    {
        tourTime += Time.deltaTime;
        float cycle = (tourTime % 56) / 7;
        int a = Mathf.FloorToInt(cycle) % tourPositions.Length;
        int b = (a + 1) % tourPositions.Length;
        int previous = (a + tourPositions.Length - 1) % tourPositions.Length;
        int next = (b + 1) % tourPositions.Length;
        float t = cycle - Mathf.Floor(cycle);
        Vector3 position = CatmullRom(tourPositions[previous], tourPositions[a], tourPositions[b], tourPositions[next], t);
        Vector3 target = Vector3.Lerp(tourTargets[a], tourTargets[b], Mathf.SmoothStep(0, 1, t));
        transform.rotation = Quaternion.identity;
        transform.position = position - cameraHome;
        viewCamera.transform.localPosition = cameraHome;
        viewCamera.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return .5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }

    public void ResetPlayer()
    {
        if (TourActive) ToggleTour();
        controller.enabled = false;
        transform.SetPositionAndRotation(initialPosition, initialRotation);
        yaw = initialRotation.eulerAngles.y;
        pitch = 0;
        viewCamera.transform.localPosition = cameraHome;
        viewCamera.transform.localRotation = Quaternion.identity;
        verticalSpeed = 0;
        controller.enabled = true;
        ShowNotice("Back at Ocean Drive");
    }

    bool NearRadio()
    {
        if (!interactionPoint) return false;
        Vector3 delta = transform.position - interactionPoint.position;
        delta.y = 0;
        return delta.sqrMagnitude <= 25;
    }

    public void ToggleRadio()
    {
        if (!interactionPoint) return;
        if (!radio)
        {
            var sourceObject = new GameObject("Diner Radio - Original Synth Loop");
            sourceObject.transform.SetParent(interactionPoint, false);
            radio = sourceObject.AddComponent<AudioSource>();
            radioClip = CreateSynthLoop();
            radio.clip = radioClip;
            radio.loop = true;
            radio.spatialBlend = 1;
            radio.rolloffMode = AudioRolloffMode.Linear;
            radio.minDistance = 5;
            radio.maxDistance = 36;
            radio.volume = .28f;
        }
        if (radio.isPlaying) radio.Pause(); else radio.Play();
        InteractionCount++;
        ShowNotice(radio.isPlaying ? "Harbor FM  /  original sunset synth" : "Harbor FM  /  radio off");
    }

    AudioClip CreateSynthLoop()
    {
        const int sampleRate = 22050;
        const double beat = 60.0 / 112;
        int samples = (int)(16 * beat * sampleRate);
        var data = new float[samples];
        int[] roots = { 45, 41, 48, 43 };
        int[] intervals = { 0, 7, 12, 7, 3, 7, 15, 7 };
        for (int i = 0; i < samples; i++)
        {
            double time = i / (double)sampleRate;
            double beats = time / beat;
            int chord = ((int)(beats / 4)) % 4;
            double beatPhase = beats - Math.Floor(beats);
            double stepPhase = beats * 2 - Math.Floor(beats * 2);
            int interval = intervals[((int)(beats * 2)) % intervals.Length];
            if (chord == 1 || chord == 2 || chord == 3) { if (interval == 3) interval = 4; if (interval == 15) interval = 16; }
            double bassFreq = 440 * Math.Pow(2, (roots[chord] - 69) / 12.0);
            double arpFreq = 440 * Math.Pow(2, (roots[chord] + 12 + interval - 69) / 12.0);
            double arp = Math.Sin(time * arpFreq * Math.PI * 2) * Math.Exp(-stepPhase * 5) * .11;
            double bass = Math.Sin(time * bassFreq * Math.PI * 2) * Math.Exp(-beatPhase * 2) * .12;
            double kick = Math.Sin(beatPhase * (52 + 75 * Math.Exp(-beatPhase * 22)) * Math.PI * 2) * Math.Exp(-beatPhase * 17) * .15;
            double noise = Math.Sin(i * 12.9898) * 43758.5453;
            noise = (noise - Math.Floor(noise)) * 2 - 1;
            double hat = noise * Math.Exp(-stepPhase * 38) * .018;
            // Fade the ends very briefly to avoid clicks when the loop wraps.
            float edge = Mathf.Min(1, Mathf.Min(i / 180f, (samples - 1 - i) / 180f));
            data[i] = (float)(arp + bass + kick + hat) * edge;
        }
        var clip = AudioClip.Create("Harbor FM - Original Procedural Synth", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    IEnumerator CapturePhoto()
    {
        captureBusy = true;
        hud.enabled = false;
        yield return new WaitForEndOfFrame();
        string folder = Path.Combine(Application.persistentDataPath, "PalmHarborPhotos");
        bool saved = false;
        Texture2D photo = null;
        try
        {
            Directory.CreateDirectory(folder);
            LastPhotoPath = Path.Combine(folder, "PalmHarbor_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            photo = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(LastPhotoPath, photo.EncodeToPNG());
            saved = true;
            Debug.Log("Palm Harbor photo saved: " + LastPhotoPath);
        }
        catch (Exception exception) { Debug.LogWarning("Photo could not be saved: " + exception.Message); }
        finally { if (photo) Destroy(photo); }
        yield return null;
        hud.enabled = hudVisible;
        captureBusy = false;
        ShowNotice(saved ? "Photo saved  /  " + folder : "Photo could not be saved; see Console");
    }

    void BuildHud()
    {
        var canvasObject = new GameObject("Palm Harbor HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        hud = canvasObject.GetComponent<Canvas>();
        hud.renderMode = RenderMode.ScreenSpaceOverlay;
        hud.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title = MakeText("Location", new Vector2(0, 1), new Vector2(38, -30), new Vector2(600, 44), 30, TextAnchor.UpperLeft);
        title.text = "PALM HARBOR";
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1,.88f,.8f);
        var subtitle = MakeText("District", new Vector2(0, 1), new Vector2(40, -78), new Vector2(650, 28), 14, TextAnchor.UpperLeft);
        subtitle.text = "O C E A N   D R I V E   /   1 9 8 6";
        subtitle.color = new Color(.64f,.94f,.94f);
        status = MakeText("Time", new Vector2(1, 1), new Vector2(-38, -38), new Vector2(370, 40), 16, TextAnchor.UpperRight);
        reticle = MakeText("Reticle", new Vector2(.5f,.5f), Vector2.zero, new Vector2(16, 24), 18, TextAnchor.MiddleCenter);
        reticle.text = "+";
        reticle.color = new Color(1,1,1,.6f);
        var controls = MakeText("Controls", new Vector2(.5f,0), new Vector2(0,28), new Vector2(1780,40), 17, TextAnchor.MiddleCenter);
        controls.text = "WASD  walk     MOUSE  look     SHIFT  sprint     SPACE  jump     N  sunset / night     F  cinematic tour     P  photo     H  hide HUD     R  reset     ESC  cursor";
        controls.color = new Color(.91f,.95f,1,.88f);
        prompt = MakeText("Interaction", new Vector2(.5f,0), new Vector2(0,100), new Vector2(1000,45), 22, TextAnchor.MiddleCenter);
        prompt.color = new Color(1,.83f,.63f);
        notice = MakeText("Notice", new Vector2(.5f,1), new Vector2(0,-46), new Vector2(1100,55), 19, TextAnchor.MiddleCenter);
        notice.color = new Color(.78f,1,1);
    }

    UnityEngine.UI.Text MakeText(string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(hud.transform, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.GetComponent<UnityEngine.UI.Text>();
        if (hudFont) text.font = hudFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        var outline = go.GetComponent<UnityEngine.UI.Outline>();
        outline.effectDistance = new Vector2(1,-1);
        outline.effectColor = new Color32(14,18,35,210);
        return text;
    }

    void UpdateHud()
    {
        status.text = TourActive ? "CINEMATIC TOUR  /  F TO EXPLORE" : IsNight ? "AFTER DARK  /  21:00" : "GOLDEN HOUR  /  18:42";
        prompt.text = !TourActive && NearRadio() ? "[ E ]   " + (radio && radio.isPlaying ? "Switch off diner radio" : "Tune in to Harbor FM") : "";
        reticle.enabled = !TourActive && Cursor.lockState == CursorLockMode.Locked;
        if (Time.unscaledTime > noticeUntil) notice.text = "";
    }

    void ShowNotice(string message)
    {
        if (!notice) return;
        notice.text = message;
        noticeUntil = Time.unscaledTime + 4;
    }

    void SetCursor(bool locked)
    {
        wantsCursor = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus && wantsCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (!focus) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        if (radioClip) Destroy(radioClip);
        if (runtimeSky)
        {
            if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
            Destroy(runtimeSky);
        }
        if (runtimeWater)
        {
            if (waterRenderer && waterRenderer.sharedMaterial == runtimeWater) waterRenderer.sharedMaterial = originalWater;
            Destroy(runtimeWater);
        }
    }

    static float NormalizeAngle(float angle) { return angle > 180 ? angle - 360 : angle; }
}
