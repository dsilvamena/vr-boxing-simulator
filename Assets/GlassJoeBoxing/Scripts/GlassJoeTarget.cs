using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stationary Glass Joe target. No AI or rig required.</summary>
[DisallowMultipleComponent]
public class GlassJoeTarget : MonoBehaviour
{
    [Header("Vida")]
    [Min(1f)] public float maxHealth = 100f;
    [SerializeField] float currentHealth = 100f;
    [Min(0f)] public float damagePerHit = 10f;
    [Min(0f)] public float hitCooldown = 0.18f;

    [Header("Respuesta visual opcional")]
    [Tooltip("Arrastra el modelo visible. Si queda vacío se usa este objeto.")]
    public Transform visualRoot;
    [Min(0f)] public float recoilDistance = 0.035f;
    [Min(0f)] public float recoilDegrees = 5f;
    [Min(0.1f)] public float recoilReturnSpeed = 9f;

    [Header("Barra de vida")]
    public bool createWorldHealthBar = true;
    public Vector3 healthBarOffset = new Vector3(0f, 2.1f, 0f);

    public float CurrentHealth => currentHealth;
    public bool IsKnockedOut => currentHealth <= 0f;

    Vector3 visualStartPos;
    Quaternion visualStartRot;
    float lastHitTime = -999f;
    float recoilTimer;
    Vector3 recoilPos;
    Quaternion recoilRot;
    Transform healthBarRoot;
    Slider healthSlider;
    Text healthText;
    Camera cachedCamera;
    readonly Dictionary<int, float> lastHitByFist = new Dictionary<int, float>();

    void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        visualStartPos = visualRoot.localPosition;
        visualStartRot = visualRoot.localRotation;
        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        recoilPos = visualStartPos;
        recoilRot = visualStartRot;
        cachedCamera = Camera.main;
        if (createWorldHealthBar) CreateHealthBar();
        UpdateHealthBar();
    }

    void LateUpdate()
    {
        if (visualRoot != null)
        {
            recoilTimer = Mathf.MoveTowards(recoilTimer, 0f, Time.deltaTime * recoilReturnSpeed);
            float t = Mathf.Clamp01(recoilTimer);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.Lerp(visualStartPos, recoilPos, t), Time.deltaTime * recoilReturnSpeed);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.Slerp(visualStartRot, recoilRot, t), Time.deltaTime * recoilReturnSpeed);
        }
        if (healthBarRoot != null)
        {
            healthBarRoot.position = transform.position + healthBarOffset;
            if (cachedCamera == null) cachedCamera = Camera.main;
            if (cachedCamera != null) healthBarRoot.rotation = Quaternion.LookRotation(cachedCamera.transform.position - healthBarRoot.position);
        }
    }

    public bool ReceivePunch(Vector3 hitPosition, Vector3 punchDirection, float damage = -1f, int fistId = 0)
    {
        if (IsKnockedOut || Time.time - lastHitTime < hitCooldown) return false;
        if (fistId != 0 && lastHitByFist.TryGetValue(fistId, out float last) && Time.time - last < hitCooldown) return false;
        lastHitTime = Time.time;
        if (fistId != 0) lastHitByFist[fistId] = Time.time;

        float appliedDamage = damage < 0f ? damagePerHit : damage;
        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, appliedDamage));
        TriggerRecoil(hitPosition, punchDirection);
        UpdateHealthBar();
        Debug.Log($"Glass Joe recibió un golpe. Vida: {currentHealth:0}/{maxHealth:0}", this);
        if (IsKnockedOut) Debug.Log("Glass Joe quedó fuera de combate.", this);
        return true;
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        lastHitTime = -999f;
        lastHitByFist.Clear();
        UpdateHealthBar();
    }

    void TriggerRecoil(Vector3 hitPosition, Vector3 punchDirection)
    {
        if (visualRoot == null) return;
        Vector3 localAway = transform.InverseTransformDirection(-punchDirection.normalized);
        if (localAway.sqrMagnitude < 0.001f) localAway = Vector3.back;
        recoilPos = visualStartPos + new Vector3(localAway.x, 0f, localAway.z).normalized * recoilDistance;
        Vector3 localHit = transform.InverseTransformPoint(hitPosition);
        float side = localHit.x >= 0f ? -1f : 1f;
        recoilRot = visualStartRot * Quaternion.Euler(-recoilDegrees, 0f, side * recoilDegrees * 0.6f);
        recoilTimer = 1f;
    }

    void CreateHealthBar()
    {
        GameObject canvasGo = new GameObject("GlassJoeHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        healthBarRoot = canvasGo.transform;
        healthBarRoot.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220f, 46f);
        canvasRect.localScale = Vector3.one * 0.01f;

        GameObject back = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(canvasGo.transform, false);
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0.05f); backRect.anchorMax = new Vector2(1f, 0.55f);
        backRect.offsetMin = Vector2.zero; backRect.offsetMax = Vector2.zero;
        back.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        GameObject fillArea = new GameObject("HealthSlider", typeof(RectTransform), typeof(Slider));
        fillArea.transform.SetParent(canvasGo.transform, false);
        RectTransform sliderRect = fillArea.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.025f, 0.1f); sliderRect.anchorMax = new Vector2(0.975f, 0.5f);
        sliderRect.offsetMin = Vector2.zero; sliderRect.offsetMax = Vector2.zero;
        healthSlider = fillArea.GetComponent<Slider>();
        healthSlider.minValue = 0f; healthSlider.maxValue = maxHealth; healthSlider.interactable = false;
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.82f, 0.08f, 0.08f, 1f);
        healthSlider.fillRect = fillRect;
        healthSlider.targetGraphic = fill.GetComponent<Image>();

        GameObject label = new GameObject("HealthLabel", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(canvasGo.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.57f); labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
        healthText = label.GetComponent<Text>();
        healthText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        healthText.fontSize = 24; healthText.alignment = TextAnchor.MiddleCenter;
        healthText.color = Color.white; healthText.text = "GLASS JOE";
    }

    void UpdateHealthBar()
    {
        if (healthSlider != null) { healthSlider.maxValue = maxHealth; healthSlider.value = currentHealth; }
        if (healthText != null) healthText.text = IsKnockedOut ? "K.O." : $"GLASS JOE  {currentHealth:0}/{maxHealth:0}";
    }
}
