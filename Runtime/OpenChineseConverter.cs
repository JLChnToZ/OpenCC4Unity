using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using OpenCC.Unity.Utils;

namespace OpenCC.Unity {
  public class OpenChineseConverter {
    protected readonly Dictionary<OpenCCDictonary, (string, string)[]> dictionaries;

    protected readonly Lazy<AhoCorasick<char, string>>
      stPhrasesCharacters, tsPhrasesCharacters,
      hkVariantsRevPhrases, hkVariants,
      twRevPhrasesVariants, twRevPhrases, twPhrases, twVariants, twRevVariants,
      jpRevPhrasesCharacters, jpVariants;

    public OpenChineseConverter() {
      dictionaries = new Dictionary<OpenCCDictonary, (string, string)[]>();
      stPhrasesCharacters = new Lazy<AhoCorasick<char, string>>(GetSTPhrasesCharacters);
      tsPhrasesCharacters = new Lazy<AhoCorasick<char, string>>(GetTSPhrasesCharacters);
      hkVariantsRevPhrases = new Lazy<AhoCorasick<char, string>>(GetHKVariantsRevPhrases);
      hkVariants = new Lazy<AhoCorasick<char, string>>(GetHKVariants);
      twRevPhrasesVariants = new Lazy<AhoCorasick<char, string>>(GetTWRevPhrasesVariants);
      twRevPhrases = new Lazy<AhoCorasick<char, string>>(GetTWRevPhrases);
      twPhrases = new Lazy<AhoCorasick<char, string>>(GetTWPhrases);
      twVariants = new Lazy<AhoCorasick<char, string>>(GetTWVariants);
      twRevVariants = new Lazy<AhoCorasick<char, string>>(GetTWRevVariants);
      jpRevPhrasesCharacters = new Lazy<AhoCorasick<char, string>>(GetJPRevPhrasesCharacters);
      jpVariants = new Lazy<AhoCorasick<char, string>>(GetJPVariants);
    }

    protected void AddDictionary(OpenCCDictonary key, string value) {
      dictionaries[key] = value.Split('\n').SelectMany(AddToDictionary).ToArray();
    }

    protected static IEnumerable<(string, string)> AddToDictionary(string mapping) {
      int index = mapping.IndexOf('\t');
      if (index <= 0) yield break;
      var main = mapping.Substring(0, index);
      foreach (var variant in mapping.Substring(index + 1).Split(' '))
        if (!string.IsNullOrEmpty(variant))
          yield return (main, variant);
    }

    public AhoCorasick<char, string> GetReplacer(params (OpenCCDictonary name, bool isReverse)[] entries) {
      var replacer = new AhoCorasick<char, string>();
      var temp = new HashSet<string>();
      foreach (var (name, isReverse) in entries) {
        temp.Clear();
        if (dictionaries.TryGetValue(name, out var srcMapping))
          foreach (var (first, second) in srcMapping) {
            if (isReverse) replacer[second] = first;
            else if (temp.Add(first)) replacer[first] = second;
          }
      }
      return replacer;
    }

    public void LoadFromResources(string prefix = "OpenCCDictionaries/") {
      foreach (var key in Enum.GetValues(typeof(OpenCCDictonary)) as OpenCCDictonary[]) {
        var cacheKey = key;
        var req = Resources.LoadAsync<TextAsset>($"{prefix}{key}");
        req.completed += value => {
          if (req.asset == null) {
            Debug.LogError($"{prefix}{key} does not exists");
            return;
          }
          if (value.isDone) AddDictionary(cacheKey, (req.asset as TextAsset).text);
        };
      }
    }

    private AhoCorasick<char, string> GetSTPhrasesCharacters() => GetReplacer(
      (OpenCCDictonary.STPhrases, false),
      (OpenCCDictonary.STCharacters, false)
    );

    private AhoCorasick<char, string> GetTSPhrasesCharacters() => GetReplacer(
      (OpenCCDictonary.TSPhrases, false),
      (OpenCCDictonary.TSCharacters, false)
    );

    private AhoCorasick<char, string> GetHKVariantsRevPhrases() => GetReplacer(
      (OpenCCDictonary.HKVariantsRevPhrases, false),
      (OpenCCDictonary.HKVariants, true)
    );

    private AhoCorasick<char, string> GetHKVariants() => GetReplacer(
      (OpenCCDictonary.HKVariants, false)
    );

    private AhoCorasick<char, string> GetTWRevPhrasesVariants() => GetReplacer(
      (OpenCCDictonary.TWPhrasesIT, true),
      (OpenCCDictonary.TWPhrasesName, true),
      (OpenCCDictonary.TWPhrasesOther, true),
      (OpenCCDictonary.TWVariantsRevPhrases, false),
      (OpenCCDictonary.TWVariants, true)
    );

    private AhoCorasick<char, string> GetTWRevPhrases() => GetReplacer(
      (OpenCCDictonary.TWVariantsRevPhrases, false),
      (OpenCCDictonary.TWVariants, true)
    );

    private AhoCorasick<char, string> GetTWPhrases() => GetReplacer(
      (OpenCCDictonary.TWPhrasesIT, false),
      (OpenCCDictonary.TWPhrasesName, false),
      (OpenCCDictonary.TWPhrasesOther, false)
    );

    private AhoCorasick<char, string> GetTWVariants() => GetReplacer(
      (OpenCCDictonary.TWVariants, false)
    );

    private AhoCorasick<char, string> GetTWRevVariants() => GetReplacer(
      (OpenCCDictonary.TWVariantsRevPhrases, false),
      (OpenCCDictonary.TWVariants, true)
    );

    private AhoCorasick<char, string> GetJPRevPhrasesCharacters() => GetReplacer(
      (OpenCCDictonary.JPShinjitaiPhrases, false),
      (OpenCCDictonary.JPShinjitaiCharacters, false),
      (OpenCCDictonary.JPVariants, true)
    );

    private AhoCorasick<char, string> GetJPVariants() => GetReplacer(
      (OpenCCDictonary.JPVariants, false)
    );

    /// <summary>
    /// 簡體到繁體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string S2T(string text) => text
      .Replace(stPhrasesCharacters.Value);

    /// <summary>
    /// 繁體到簡體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string T2S(string text) => text
      .Replace(tsPhrasesCharacters.Value);

    /// <summary>
    /// 簡體到臺灣正體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string S2TW(string text) => text
      .Replace(stPhrasesCharacters.Value)
      .Replace(twVariants.Value);

    /// <summary>
    /// 臺灣正體到簡體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string TW2S(string text) => text
      .Replace(twRevPhrases.Value)
      .Replace(tsPhrasesCharacters.Value);

    /// <summary>
    /// 簡體到香港繁體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string S2HK(string text) => text
      .Replace(stPhrasesCharacters.Value)
      .Replace(hkVariants.Value);

    /// <summary>
    /// 香港繁體到簡體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>

    public string HK2S(string text) => text
      .Replace(hkVariantsRevPhrases.Value)
      .Replace(tsPhrasesCharacters.Value);

    /// <summary>
    /// 簡體到繁體（臺灣正體標準）並轉換爲臺灣常用詞彙
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string S2TWP(string text) => text
      .Replace(stPhrasesCharacters.Value)
      .Replace(twPhrases.Value)
      .Replace(twVariants.Value);

    /// <summary>
    /// 繁體（臺灣正體標準）到簡體並轉換爲中國大陸常用詞彙
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string TW2SP(string text) => text
      .Replace(twRevPhrasesVariants.Value)
      .Replace(tsPhrasesCharacters.Value);

    /// <summary>
    /// 繁體（OpenCC 標準）到台灣正體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string T2TW(string text) => text
      .Replace(twVariants.Value);

    /// <summary>
    /// 香港繁體到繁體（OpenCC 標準）
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string HK2T(string text) => text
      .Replace(hkVariantsRevPhrases.Value);

    /// <summary>
    /// 繁體（OpenCC 標準）到香港繁體
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string T2HK(string text) => text
      .Replace(hkVariants.Value);

    /// <summary>
    /// 日文新字體到繁體（OpenCC 標準，舊字體）
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string JP2T(string text) => text
      .Replace(jpRevPhrasesCharacters.Value);

    /// <summary>
    /// 日文新字體到繁體（OpenCC 標準，舊字體）
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string T2JP(string text) => text
      .Replace(jpVariants.Value);

    /// <summary>
    /// 臺灣正體到繁體（OpenCC 標準）
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string TW2T(string text) => text
      .Replace(twRevVariants.Value);
  }

  public enum OpenCCDictonary {
    HKVariants,
    HKVariantsRevPhrases,
    JPVariants,
    JPShinjitaiPhrases,
    JPShinjitaiCharacters,
    STCharacters,
    STPhrases,
    TSCharacters,
    TSPhrases,
    TWPhrasesIT,
    TWPhrasesName,
    TWPhrasesOther,
    TWVariants,
    TWVariantsRevPhrases,
  }
}
