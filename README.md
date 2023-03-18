# OpenCC4Unity

This is [Open Chinese Convert](https://github.com/BYVoid/OpenCC/) ported to Unity C#.

## Usage

```csharp
using OpenCC.Unity;

var converter = new OpenChineseConverter();
convert.LoadFromResources();

var convertedText = convert.T2S("這一段用繁體中文寫的文字會變成簡體");
Debug.Log(convertedText); // Output: 这一段用繁体中文写的文字会变成简体
```

## Available APIs

```csharp
// Simplified Chinese to Traditional Chinese 簡體到繁體
converter.S2T("...");
// Traditional Chinese to Simplified Chinese 繁體到簡體
converter.T2S("...");
// Simplified Chinese to Traditional Chinese (Taiwan Standard) 簡體到臺灣正體
converter.S2TW("...");
// Traditional Chinese (Taiwan Standard) to Simplified Chinese 臺灣正體到簡體
converter.TW2S("...");
// Simplified Chinese to Traditional Chinese (Hong Kong variant) 簡體到香港繁體
converter.S2HK("...");
// Traditional Chinese (Hong Kong variant) to Simplified Chinese 香港繁體到簡體
converter.HK2S("...");
// Simplified Chinese to Traditional Chinese (Taiwan Standard) with Taiwanese idiom 簡體到繁體（臺灣正體標準）並轉換爲臺灣常用詞彙
converter.S2TWP("...");
// Traditional Chinese (Taiwan Standard) to Simplified Chinese with Mainland Chinese idiom 繁體（臺灣正體標準）到簡體並轉換爲中國大陸常用詞彙
converter.TW2SP("...");
// Traditional Chinese (OpenCC Standard) to Taiwan Standard 繁體（OpenCC 標準）到臺灣正體
converter.T2TW("...");
// Traditional Chinese (Hong Kong variant) to Traditional Chinese 香港繁體到繁體（OpenCC 標準）
converter.HK2T("...");
// Traditional Chinese (OpenCC Standard) to Hong Kong variant 繁體（OpenCC 標準）到香港繁體
converter.T2HK("...");
// Traditional Chinese Characters (Kyūjitai) to New Japanese Kanji (Shinjitai) 繁體（OpenCC 標準，舊字體）到日文新字體
converter.T2JP("...");
// New Japanese Kanji (Shinjitai) to Traditional Chinese Characters (Kyūjitai) 日文新字體到繁體（OpenCC 標準，舊字體）
converter.JP2T("...");
// Traditional Chinese (Taiwan standard) to Traditional Chinese 臺灣正體到繁體（OpenCC 標準）
converter.TW2T("...");
```

## Installation

Use the Unity package manager, press the `+` button at the top-left corner, select "Add package from git URL", paste this git repository URL: `https://github.com/JLChnToZ/OpenCC4Unity.git`.


## License

[MIT](LICENSE), also contains third-party asset, see [this notice](Third%20Party%20Notices.md).
