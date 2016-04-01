﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Japanese_To_Romaji_File_Converter {
    public class Converter {

        public static string[] StartSrcSplit = new string[] { "<div id=src-translit class=translit dir=ltr style=\"text-align:;display:block\">" };
        public static string[] EndSrcSplit = new string[] { "</div>" };
        public static string LanguagePair = "ja|en";
        public static char MapChar = '`';

        public event EventHandler<ProgressEventArgs> Progress;

        private List<string> Files;

        public Converter(List<string> files) {
            Files = files;
        }

        public void Convert() {
            string sep = "|";
            string innerSep = ":";

            foreach (string filePath in Files) {
                if (!File.Exists(filePath)) {
                    // TODO Error
                    continue;
                }

                // Get file details
                string directoryPath = Path.GetDirectoryName(filePath);
                string extension = Path.GetExtension(filePath);
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                // Get tags
                TagLib.File tagFile = TagLib.File.Create(filePath);
                string title = tagFile.Tag.Title;
                string performers = String.Join(innerSep, tagFile.Tag.Performers);
                string albumArtists = String.Join(innerSep, tagFile.Tag.AlbumArtists);
                string album = tagFile.Tag.Album;

                // Translate everything in one request
                string translateText = fileName + sep + title + sep + performers + sep + albumArtists + sep + album;
                string translatedText = Translate(translateText);

                // Parse translated text into individual parts
                string[] translationParts = translatedText.Split(new string[] { sep }, StringSplitOptions.None);
                string newFileName = translationParts[0].Trim();
                title = translationParts[1].Trim();
                performers = translationParts[2].Trim();
                albumArtists = translationParts[3].Trim();
                album = translationParts[4].Trim();

                // Set new tags
                tagFile.Tag.Title = title;
                tagFile.Tag.Performers = performers.Split(new string[] { innerSep }, StringSplitOptions.None);
                tagFile.Tag.AlbumArtists = albumArtists.Split(new string[] { innerSep }, StringSplitOptions.None);
                tagFile.Save();

                // Move file to new path
                string newFilePath = directoryPath + Path.DirectorySeparatorChar + newFileName + extension;
                // TODO exception
                File.Move(filePath, newFilePath);

                // Update progress
                OnProgressUpdate(ProgressUpdate.Converted, fileName + extension, newFileName + extension);
            }
            OnProgressUpdate(ProgressUpdate.Completed);
        }

        private string Translate(string inText) {
            // Map english characters to substitutes
            Tuple<string, List<char>> charMap = MapChars(inText);
            inText = charMap.Item1;

            // Get translation
            WebClient webClient = new WebClient();
            webClient.Encoding = Encoding.UTF8;
            string url = String.Format("https://www.google.com/translate_t?hl=en&ie=UTF8&text={0}&langpair={1}",
                                        inText, LanguagePair);
            string src = webClient.DownloadString(url);

            // Get translation from source code between two strings
            string outText = src.Split(StartSrcSplit, StringSplitOptions.None).Last()
                                       .Split(EndSrcSplit, StringSplitOptions.None).First();

            // Unmap english characters back from substitutes
            outText = UnmapChars(outText, charMap.Item2);

            return outText;
        }

        public static bool IsAscii(string value) {
            // ASCII encoding replaces non-ascii with question marks, so we use UTF8 to see if multi-byte sequences are there
            return Encoding.UTF8.GetByteCount(value) == value.Length;
        }

        private Tuple<string, List<char>> MapChars(string text) {
            // Replace characters with sub chars for the translation
            List<char> mapChars = new List<char>();
            StringBuilder mapText = new StringBuilder(text);

            // Loop through each character and map english with $MapChar
            for (int i = 0; i < text.Length; i++) {
                char currChar = text[i];
                if (IsAscii(currChar.ToString())) {
                    mapChars.Add(currChar);
                    mapText[i] = MapChar;
                } else if (currChar == MapChar) {
                    mapChars.Add(currChar);
                }
            }

            return Tuple.Create(mapText.ToString(), mapChars);
        }

        private string UnmapChars(string text, List<char> mapChars) {
            StringBuilder unmapText = new StringBuilder(text);

            // Loop through each character and unmap mapped chars
            int mapIndex = 0;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] == MapChar) {
                    unmapText[i] = mapChars[mapIndex++];
                }
            }

            return unmapText.ToString();
        }

        private void OnProgressUpdate(ProgressUpdate type, string oldFileName = null, string newFileName = null) {
            Progress(this, new ProgressEventArgs(type, oldFileName, newFileName));
        }

    }

    public enum ProgressUpdate {
        Converted,
        Completed
    }

    public class ProgressEventArgs : EventArgs {

        public ProgressUpdate Type;
        public string OldFileName;
        public string NewFileName;
        
        public ProgressEventArgs(ProgressUpdate type, string oldFileName = null, string newFileName = null) {
            Type = type;
            OldFileName = oldFileName;
            NewFileName = newFileName;
        }

    }

}