using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using UnityEngine.TextCore.Text;

/// <summary>
/// This component works with the Dialogue System's UILocalizationManager to change
/// fonts in a UIDocument when the language changes.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UIToolkitFontLocalizer : MonoBehaviour
{
    [System.Serializable]
    public class FontMapping
    {
        [Tooltip("The language code (e.g., 'ko', 'ja')")]
        public string language;
        [Tooltip("The FontAsset for this language")]
        public FontAsset fontAsset;
    }

    [Tooltip("The default font to use if a language-specific font isn't found.")]
    [SerializeField]
    private FontAsset m_defaultFont;

    [Tooltip("The list of fonts to use for specific languages.")]
    [SerializeField]
    private List<FontMapping> m_fontMappings = new List<FontMapping>();

    private UIDocument m_uiDocument;

    private void Awake()
    {
        m_uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        // Subscribe to the language changed event.
        UILocalizationManager.languageChanged += UpdateFonts;
        // Also apply fonts immediately on enable.
        if (UILocalizationManager.instance != null)
        {
            UpdateFonts(UILocalizationManager.instance.currentLanguage);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from the event.
        UILocalizationManager.languageChanged -= UpdateFonts;
    }

    /// <summary>
    /// This method is called by UILocalizationManager when the language changes.
    /// </summary>
    /// <param name="language">The new language code.</param>
    public void UpdateFonts(string language)
    {
        if (m_uiDocument == null || m_uiDocument.rootVisualElement == null)
        {
            if (Debug.isDebugBuild) Debug.LogWarning("UIToolkitFontLocalizer: UIDocument is not ready.", this);
            return;
        }

        // Find the correct font for the new language.
        FontAsset targetFont = m_defaultFont;
        var mapping = m_fontMappings.FirstOrDefault(m => m.language == language);
        if (mapping != null && mapping.fontAsset != null)
        {
            targetFont = mapping.fontAsset;
        }

        if (targetFont == null)
        {
            if (Debug.isDebugBuild) Debug.LogWarning($"UIToolkitFontLocalizer: No font found for language '{language}' and no default font is set.", this);
            return;
        }

        // Find all Label elements in the UIDocument.
        var labels = m_uiDocument.rootVisualElement.Query<Label>().ToList();

        // Apply the new font to all labels.
        foreach (var label in labels)
        {
            label.style.unityFontDefinition = new StyleFontDefinition(targetFont);
        }
         if (Debug.isDebugBuild) Debug.Log($"UIToolkitFontLocalizer: Applied font '{targetFont.name}' for language '{language}' to {labels.Count} labels.", this);
    }
}
