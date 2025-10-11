using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TPMenuSystem: A ready-to-use, flexible Unity UI menu manager.
/// Attach this to a GameObject (ideally under a Canvas). Configure pages in the inspector.
/// Supports: open/close, page switching, fade animations (unscaled time), input toggle, pausing time, and events.
/// </summary>
public sealed class TPMenuSystem : MonoBehaviour
{
    [Serializable]
    public sealed class StringEvent : UnityEvent<string> { }

    [Serializable]
    public sealed class MenuPage
    {
        [Tooltip("Unique key used to show this page via ShowPage(key)")]
        public string key;

        [Tooltip("Root GameObject for this page (enable/disable will control visibility)")]
        public GameObject root;

        [Tooltip("UI element to select when this page becomes active (optional)")]
        public Selectable defaultSelected;

        [Tooltip("If true, page's GameObject is deactivated on hide; otherwise only CanvasGroup is faded")]
        public bool deactivateOnHide = true;

        [Tooltip("Optional: If set, page will fade using this CanvasGroup. If absent and deactivateOnHide=false, a CanvasGroup will be added at runtime.")]
        public CanvasGroup canvasGroup;

        [Tooltip("Optional: Override fade duration for this page only (-1 to use global)")]
        public float fadeDurationOverride = -1f;
    }

    public static TPMenuSystem Instance { get; private set; }

    [Header("Setup")]
    [Tooltip("CanvasGroup controlling the whole menu visibility and interactivity")]
    public CanvasGroup menuCanvasGroup;

    [Tooltip("Optional background dimmer Image. If set and closeOnBackgroundClick=true, add a Button and wire OnBackgroundClick.")]
    public Image backgroundDimmer;

    [Tooltip("List of menu pages managed by this system")]
    public List<MenuPage> pages = new List<MenuPage>();

    [Tooltip("Initial page key to show on first open (leave empty to use the first page)")]
    public string initialPageKey;

    [Header("Behavior")] 
    [Tooltip("Start hidden on play")]
    public bool startHidden = true;

    [Tooltip("Keep this menu across scene loads")]
    public bool dontDestroyOnLoad = false;

    [Tooltip("Pause Time.timeScale while menu is open")]
    public bool pauseWhileOpen = true;

    [Tooltip("Block raycasts while menu is open")]
    public bool blockRaycastsWhileOpen = true;

    [Tooltip("Fade duration for menu open/close (seconds, unscaled time)")]
    [Min(0f)] public float fadeDuration = 0.15f;

    [Tooltip("Close menu when background (dimmer) is clicked")] 
    public bool closeOnBackgroundClick = false;

    [Header("Input")] 
    [Tooltip("Enable built-in input toggle handling")]
    public bool enableInputToggle = true;

    [Tooltip("Key to toggle the menu open/closed")] 
    public KeyCode toggleKey = KeyCode.Escape;

    [Tooltip("Re-focus default selected when page opens")]
    public bool setFirstSelectedOnOpen = true;

    [Header("Events")] 
    public UnityEvent onMenuOpened;
    public UnityEvent onMenuClosed;
    public StringEvent onPageShown;

    private readonly Dictionary<string, MenuPage> keyToPage = new Dictionary<string, MenuPage>(StringComparer.Ordinal);
    private string currentPageKey = null;
    private bool isInitialized = false;
    private bool isOpen = false;
    private Coroutine menuFadeRoutine = null;
    private EventSystem cachedEventSystem;
    private GameObject selectionBeforeOpen;

    private float backgroundDimmerOriginalAlpha = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("TPMenuSystem: Multiple instances detected. The newest instance will take over as singleton.");
        }
        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        cachedEventSystem = EventSystem.current;
        if (cachedEventSystem == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
            cachedEventSystem = esGo.GetComponent<EventSystem>();
            DontDestroyOnLoad(esGo);
        }

        if (menuCanvasGroup == null)
        {
            menuCanvasGroup = GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
            {
                menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (backgroundDimmer != null)
        {
            backgroundDimmerOriginalAlpha = backgroundDimmer.color.a;
        }

        InitializeIfNeeded();

        if (startHidden)
        {
            ForceHideMenuImmediate();
        }
        else
        {
            ForceShowMenuImmediate();
        }
    }

    private void Update()
    {
        if (!enableInputToggle)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized)
        {
            return;
        }

        keyToPage.Clear();

        for (int i = 0; i < pages.Count; i++)
        {
            MenuPage page = pages[i];
            if (page == null || page.root == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(page.key))
            {
                page.key = page.root.name;
            }

            if (!keyToPage.ContainsKey(page.key))
            {
                keyToPage.Add(page.key, page);
            }
            else
            {
                Debug.LogWarning($"TPMenuSystem: Duplicate page key '{page.key}' ignored.");
            }

            // Prepare CanvasGroup if using non-deactivating transitions
            if (!page.deactivateOnHide)
            {
                if (page.canvasGroup == null)
                {
                    page.canvasGroup = page.root.GetComponent<CanvasGroup>();
                    if (page.canvasGroup == null)
                    {
                        page.canvasGroup = page.root.AddComponent<CanvasGroup>();
                    }
                }
            }

            // Ensure all pages start hidden at init
            SetPageVisible(page, visible: false, immediate: true);
        }

        isInitialized = true;
    }

    public void ToggleMenu()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        InitializeIfNeeded();

        if (isOpen)
        {
            return;
        }

        isOpen = true;
        selectionBeforeOpen = cachedEventSystem != null ? cachedEventSystem.currentSelectedGameObject : null;

        if (pauseWhileOpen)
        {
            Time.timeScale = 0f;
        }

        string pageToShow = !string.IsNullOrEmpty(initialPageKey) && keyToPage.ContainsKey(initialPageKey)
            ? initialPageKey
            : FirstPageKeyOrNull();

        if (!string.IsNullOrEmpty(pageToShow))
        {
            ShowPage(pageToShow);
        }

        FadeMenu(visible: true);
        onMenuOpened?.Invoke();
    }

    public void CloseMenu()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        // Hide the current page immediately for snappier feel; the menu will fade
        if (!string.IsNullOrEmpty(currentPageKey) && keyToPage.TryGetValue(currentPageKey, out var currentPage))
        {
            SetPageVisible(currentPage, visible: false, immediate: true);
            currentPageKey = null;
        }

        FadeMenu(visible: false);

        if (pauseWhileOpen)
        {
            Time.timeScale = 1f;
        }

        // Restore selection
        if (cachedEventSystem != null && selectionBeforeOpen != null)
        {
            cachedEventSystem.SetSelectedGameObject(selectionBeforeOpen);
        }

        onMenuClosed?.Invoke();
    }

    public void ShowPage(string key)
    {
        InitializeIfNeeded();

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("TPMenuSystem.ShowPage called with null/empty key");
            return;
        }

        if (!keyToPage.TryGetValue(key, out var target))
        {
            Debug.LogWarning($"TPMenuSystem: No page registered for key '{key}'");
            return;
        }

        // Hide current page
        if (!string.IsNullOrEmpty(currentPageKey) && keyToPage.TryGetValue(currentPageKey, out var current))
        {
            if (current != target)
            {
                SetPageVisible(current, visible: false, immediate: false);
            }
        }

        // Show target page
        SetPageVisible(target, visible: true, immediate: false);
        currentPageKey = key;

        if (setFirstSelectedOnOpen)
        {
            FocusDefaultSelection(target);
        }

        onPageShown?.Invoke(key);
    }

    public void OnBackgroundClick()
    {
        if (closeOnBackgroundClick)
        {
            CloseMenu();
        }
    }

    private void FocusDefaultSelection(MenuPage page)
    {
        if (cachedEventSystem == null)
        {
            return;
        }

        GameObject toSelect = null;
        if (page != null && page.defaultSelected != null)
        {
            toSelect = page.defaultSelected.gameObject;
        }

        if (toSelect == null)
        {
            // Best effort: select first selectable on the page
            var firstSelectable = page.root != null ? page.root.GetComponentInChildren<Selectable>(includeInactive: false) : null;
            if (firstSelectable != null)
            {
                toSelect = firstSelectable.gameObject;
            }
        }

        cachedEventSystem.SetSelectedGameObject(null);
        cachedEventSystem.SetSelectedGameObject(toSelect);
    }

    private string FirstPageKeyOrNull()
    {
        foreach (var kvp in keyToPage)
        {
            return kvp.Key; // return first
        }
        return null;
    }

    private void FadeMenu(bool visible)
    {
        if (menuFadeRoutine != null)
        {
            StopCoroutine(menuFadeRoutine);
        }
        menuFadeRoutine = StartCoroutine(FadeMenuCoroutine(visible));
    }

    private IEnumerator FadeMenuCoroutine(bool visible)
    {
        // Prepare
        float startAlpha = menuCanvasGroup != null ? menuCanvasGroup.alpha : 1f;
        float endAlpha = visible ? 1f : 0f;
        float duration = fadeDuration;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.interactable = visible;
            menuCanvasGroup.blocksRaycasts = visible && blockRaycastsWhileOpen;
        }

        if (backgroundDimmer != null)
        {
            Color c = backgroundDimmer.color;
            backgroundDimmer.color = new Color(c.r, c.g, c.b, visible ? backgroundDimmerOriginalAlpha : c.a);
        }

        if (Mathf.Approximately(duration, 0f))
        {
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = endAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float a = Mathf.Lerp(startAlpha, endAlpha, t);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = a;

            if (backgroundDimmer != null)
            {
                Color c = backgroundDimmer.color;
                float targetA = visible ? backgroundDimmerOriginalAlpha : 0f;
                float currentA = Mathf.Lerp(startAlpha * backgroundDimmerOriginalAlpha, targetA, t);
                backgroundDimmer.color = new Color(c.r, c.g, c.b, currentA);
            }

            yield return null; // unscaled
        }

        if (menuCanvasGroup != null) menuCanvasGroup.alpha = endAlpha;
    }

    private void SetPageVisible(MenuPage page, bool visible, bool immediate)
    {
        if (page == null || page.root == null)
        {
            return;
        }

        if (page.deactivateOnHide)
        {
            page.root.SetActive(visible);
            if (!visible)
            {
                // Clear selection if hiding the current page
                if (cachedEventSystem != null && cachedEventSystem.currentSelectedGameObject != null &&
                    cachedEventSystem.currentSelectedGameObject.transform.IsChildOf(page.root.transform))
                {
                    cachedEventSystem.SetSelectedGameObject(null);
                }
            }
            return;
        }

        // Use CanvasGroup-based fade
        if (page.canvasGroup == null)
        {
            page.canvasGroup = page.root.GetComponent<CanvasGroup>();
            if (page.canvasGroup == null)
            {
                page.canvasGroup = page.root.AddComponent<CanvasGroup>();
            }
        }

        float target = visible ? 1f : 0f;
        if (immediate)
        {
            page.canvasGroup.alpha = target;
            page.canvasGroup.interactable = visible;
            page.canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            StartCoroutine(FadePageCoroutine(page, target));
        }
    }

    private IEnumerator FadePageCoroutine(MenuPage page, float targetAlpha)
    {
        CanvasGroup cg = page.canvasGroup;
        float start = cg.alpha;
        float end = targetAlpha;
        float duration = page.fadeDurationOverride >= 0f ? page.fadeDurationOverride : fadeDuration;

        if (Mathf.Approximately(duration, 0f))
        {
            cg.alpha = end;
            cg.interactable = end > 0.99f;
            cg.blocksRaycasts = end > 0.99f;
            yield break;
        }

        // Ensure visible while fading in
        if (end > start)
        {
            cg.blocksRaycasts = true;
        }
        else
        {
            // If fading out, disable interaction immediately
            cg.interactable = false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cg.alpha = Mathf.Lerp(start, end, t);
            yield return null;
        }

        cg.alpha = end;
        bool nowVisible = end > 0.99f;
        cg.interactable = nowVisible;
        cg.blocksRaycasts = nowVisible;
    }

    private void ForceHideMenuImmediate()
    {
        isOpen = false;
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        foreach (var kv in keyToPage)
        {
            SetPageVisible(kv.Value, visible: false, immediate: true);
        }
    }

    private void ForceShowMenuImmediate()
    {
        isOpen = true;
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = blockRaycastsWhileOpen;
        }

        string pageToShow = !string.IsNullOrEmpty(initialPageKey) && keyToPage.ContainsKey(initialPageKey)
            ? initialPageKey
            : FirstPageKeyOrNull();

        if (!string.IsNullOrEmpty(pageToShow))
        {
            ShowPage(pageToShow);
        }
    }

    // Static convenience API -------------------------------------------------

    public static void Open()
    {
        if (Instance != null) Instance.OpenMenu();
    }

    public static void Close()
    {
        if (Instance != null) Instance.CloseMenu();
    }

    public static void Toggle()
    {
        if (Instance != null) Instance.ToggleMenu();
    }

    public static void Show(string pageKey)
    {
        if (Instance != null) Instance.ShowPage(pageKey);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Collect Pages From Children (Direct)")]
    private void AutoCollectPagesFromChildren()
    {
        var newPages = new List<MenuPage>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var page = new MenuPage
            {
                key = child.name,
                root = child.gameObject,
                defaultSelected = child.GetComponentInChildren<Selectable>(includeInactive: true),
                deactivateOnHide = true,
                canvasGroup = child.GetComponent<CanvasGroup>(),
                fadeDurationOverride = -1f,
            };
            newPages.Add(page);
        }
        pages = newPages;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

