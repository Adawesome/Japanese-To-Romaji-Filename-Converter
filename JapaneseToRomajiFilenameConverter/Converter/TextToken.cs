﻿using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace JapaneseToRomajiFileConverter.Converter {
    public class TextToken {

        private static char MapSplitChar = ':';

        public TokenType Type { get; private set; }
        public string Text { get; set; }
        public string Prefix { get; set; }

        public TextToken(TokenType type, string text = "", string prefix = "") {
            Type = type;
            Text = text;
            Prefix = prefix;
        }

        // Loop through characters in a string and split them into sequential tokens
        // eg. "Cake 01. ヴァンパイア雪降る夜"
        // => ["Cake 01. ", "ヴァンパイア", "雪降る夜"]
        public static List<TextToken> GetTextTokens(string inText) {
            List<TextToken> textTokens = new List<TextToken>();

            // Start with arbitrary token type
            TokenType prevCharTokenType = TokenType.Latin;
            TokenType currCharTokenType = prevCharTokenType;

            TextToken currToken = new TextToken(currCharTokenType);

            foreach (char c in inText) {
                string cs = c.ToString();

                if (TextTranslator.IsHiragana(cs) || TextTranslator.IsKanji(cs)) {
                    // Hiragana / Kanji
                    currCharTokenType = TokenType.HiraganaKanji;
                } else if (TextTranslator.IsKatakana(cs)) {
                    // Katakana
                    currCharTokenType = TokenType.Katakana;
                } else {
                    // Latin or other
                    currCharTokenType = TokenType.Latin;
                }

                // Check if there is a new token
                if (prevCharTokenType == currCharTokenType) {
                    // Same token
                    currToken.Text += cs;
                } else {
                    // New token

                    // Modifies the prefix of the token depending on prev/curr tokens
                    // eg. Add space before curr token
                    string tokenPrefix = "";

                    if (!string.IsNullOrEmpty(currToken.Text)) {
                        // Add token to token list if there is text in it
                        textTokens.Add(currToken);

                        // Get token prefix for new token if previous token was not empty
                        if (textTokens.Count > 0) {
                            char prevLastChar = textTokens.Last().Text.Last();
                            tokenPrefix = GetTokenPrefix(prevCharTokenType,
                                                             currCharTokenType,
                                                             prevLastChar, c);
                        }
                    }

                    // Create new token
                    currToken = new TextToken(currCharTokenType, cs, tokenPrefix);

                    prevCharTokenType = currCharTokenType;
                }
            }

            // Add last token to the list
            if (!string.IsNullOrEmpty(currToken.Text)) {
                textTokens.Add(currToken);
            }

            return textTokens;
        }

        public static string GetTokenPrefix(TokenType prevType, TokenType currType,
                                            char prevLastChar, char currFirstChar) {
            string prefix = "";

            switch (currType) {
                // =========================================================================
                // Current: Latin
                // =========================================================================
                case TokenType.Latin:
                    switch (prevType) {
                        // ==============================
                        // Previous: HiraganaKanji
                        // ==============================
                        case TokenType.HiraganaKanji:
                            if (!char.IsWhiteSpace(currFirstChar) &&
                                !char.IsPunctuation(currFirstChar) &&
                                currFirstChar != '~' &&
                                currFirstChar != '-') {
                                prefix = " ";
                            }
                            break;

                        // ==============================
                        // Previous: Katakana
                        // ==============================
                        case TokenType.Katakana:
                            if (!char.IsWhiteSpace(currFirstChar) &&
                                !char.IsPunctuation(currFirstChar)) {
                                prefix = " ";
                            }
                            break;
                    }
                    break;

                // =========================================================================
                // Current: HiraganaKanji
                // =========================================================================
                case TokenType.HiraganaKanji:
                    switch (prevType) {
                        // ==============================
                        // Previous: Latin
                        // ==============================
                        case TokenType.Latin:
                            if (!char.IsWhiteSpace(prevLastChar) &&
                                prevLastChar != '~' &&
                                prevLastChar != '-') {
                                prefix = " ";
                            }
                            break;

                        // ==============================
                        // Previous: Katakana
                        // ==============================
                        case TokenType.Katakana:
                            prefix = " ";
                            break;
                    }
                    break;

                // =========================================================================
                // Current: Katakana
                // =========================================================================
                case TokenType.Katakana:
                    switch (prevType) {
                        // ==============================
                        // Previous: Latin
                        // ==============================
                        case TokenType.Latin:
                            if (!char.IsWhiteSpace(prevLastChar)) {
                                prefix = " ";
                            }
                            break;

                        // ==============================
                        // Previous: HirganaKanji
                        // ==============================
                        case TokenType.HiraganaKanji:
                            prefix = " ";
                            break;
                    }
                    break;
            }

            return prefix;
        }

        // 1. Latin - Don't translate
        // 2. Katakana - Translate to output language
        // 3. Hiragana / Kanji - Translate to phonetic
        public string Translate(List<string> maps, List<string> particles, string languagePair = TextTranslator.LanguagePair) {
            string translation = "";

            switch (Type) {
                case TokenType.HiraganaKanji: {
                    // Get phoentic text
                    string url = TextTranslator.GetTranslatorUrl(Text, languagePair);
                    HtmlDocument doc = new HtmlWeb().Load(url);
                    string phoneticText = doc.GetElementbyId("src-translit").InnerText;
                    translation = FormatTranslation(phoneticText, maps, particles);
                    break;
                }

                case TokenType.Katakana: {
                    // Get translated text
                    string url = TextTranslator.GetTranslatorUrl(Text, languagePair);
                    HtmlDocument doc = new HtmlWeb().Load(url);
                    string translatedText = doc.GetElementbyId("result_box").InnerText;
                    translation = FormatTranslation(translatedText, maps, particles);
                    break;
                }

                case TokenType.Latin:
                default: {
                    translation = Prefix + Text;
                    break;
                }
            }

            return WebUtility.HtmlDecode(translation);
        }

        private string FormatTranslation(string translatedText, List<string> maps, List<string> particles) {
            // Add prefixes, trim whitespace, and capitalise words, etc.
            string outText = "";
            switch (Type) {
                case TokenType.HiraganaKanji:
                    // Maps
                    foreach (string map in maps) {
                        string[] mapStrings = map.Split(MapSplitChar);
                        if (mapStrings.Length != 2) continue;

                        translatedText = Regex.Replace(translatedText,
                                                       mapStrings[0],
                                                       mapStrings[1],
                                                       RegexOptions.IgnoreCase);
                    }
                    // Capitalise
                    translatedText = new CultureInfo("en").TextInfo.ToTitleCase(translatedText);
                    // Particles
                    foreach (string particle in particles) {
                        translatedText = Regex.Replace(translatedText,
                                                       @"\b" + particle + @"\b",
                                                       particle,
                                                       RegexOptions.IgnoreCase);
                    }
                    // Trim and join
                    outText = Prefix + translatedText.Trim();
                    break;

                case TokenType.Katakana:
                    // Capitalise
                    translatedText = new CultureInfo("en").TextInfo.ToTitleCase(translatedText);
                    // Trim and join
                    outText = Prefix + translatedText.Trim();
                    break;

                case TokenType.Latin:
                default:
                    // Join
                    outText = Prefix + translatedText;
                    break;
            }

            return outText;
        }
    }

    public enum TokenType {
        Latin,
        HiraganaKanji,
        Katakana
    }

}