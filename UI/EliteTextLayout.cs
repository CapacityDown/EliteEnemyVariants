using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteTextLayout
{
    // Use the game's Unity 6 shaper for Arabic joining/bidi and Thai marks.
    // Apply only to this mod's text; do not mutate global TMP settings/fonts.
    internal static void Apply(TMP_Text text, EliteLanguage language)
    {
        text.useTextShaper = true;
        text.isRightToLeftText = language == EliteLanguage.Arabic;
    }

    internal static IReadOnlyList<string> Tokens(string text)
    {
        List<string> tokens = new();
        StringBuilder word = new();
        void Flush() { if (word.Length > 0) { tokens.Add(word.ToString()); word.Clear(); } }
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        bool thaiLeadingVowel = false;
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            char first = element[0];
            if (first == '\r') continue;
            if (first == '\n') { Flush(); tokens.Add("\n"); thaiLeadingVowel = false; continue; }
            if (char.IsWhiteSpace(first)) { Flush(); tokens.Add(" "); thaiLeadingVowel = false; continue; }
            if (first is >= '\u0e00' and <= '\u0e7f')
            {
                Flush();
                // Keep preposed vowels with their base, and spacing vowel signs
                // with the preceding cluster. Combining marks are already grouped.
                bool append = tokens.Count > 0 && !string.IsNullOrWhiteSpace(tokens[tokens.Count - 1]) &&
                    (thaiLeadingVowel || first is '\u0e30' or '\u0e32' or '\u0e33' or '\u0e45');
                if (append) tokens[tokens.Count - 1] += element; else tokens.Add(element);
                thaiLeadingVowel = first is >= '\u0e40' and <= '\u0e44';
            }
            else if (first is >= '\u3040' and <= '\u30ff' or >= '\u3400' and <= '\u4dbf' or
                >= '\u4e00' and <= '\u9fff' or >= '\uf900' and <= '\ufaff' or >= '\uff00' and <= '\uffef')
            { Flush(); tokens.Add(element); thaiLeadingVowel = false; }
            else { word.Append(element); thaiLeadingVowel = false; }
        }
        Flush();
        return tokens;
    }
}
