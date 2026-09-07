using System;
using System.Text;
using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public enum PlayerAvatarKind : byte
    {
        Warrior = 0,
        Ranger = 1,
        Slime = 2,
    }

    /// <summary>
    /// Reads the pre-game HTML profile and applies the same validation on the
    /// client and authoritative server. Avatars share movement and skill rules;
    /// their body hitboxes follow their silhouettes and a common feet anchor.
    /// </summary>
    public static class PlayerProfileSelection
    {
        public static string RequestedName => SanitizeName(QueryValue("playerName"), "TRAVELER");
        public static PlayerAvatarKind RequestedAvatar => ParseAvatar(QueryValue("avatar"));

        public static string SanitizeName(string value, string fallback)
        {
            string decoded = Decode(value).Trim();
            if (string.IsNullOrWhiteSpace(decoded))
            {
                return fallback;
            }

            StringBuilder safe = new StringBuilder(14);
            foreach (char character in decoded)
            {
                if (safe.Length >= 14)
                {
                    break;
                }
                bool asciiLetter = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
                bool asciiDigit = character >= '0' && character <= '9';
                if (asciiLetter || asciiDigit || character == '_' || character == '-' || character == ' ')
                {
                    safe.Append(character);
                }
            }

            string result = safe.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        public static PlayerAvatarKind SanitizeAvatar(PlayerAvatarKind avatar)
        {
            return avatar is PlayerAvatarKind.Warrior or PlayerAvatarKind.Ranger or PlayerAvatarKind.Slime
                ? avatar
                : PlayerAvatarKind.Warrior;
        }

        private static PlayerAvatarKind ParseAvatar(string value)
        {
            return Decode(value).ToLowerInvariant() switch
            {
                "female" => PlayerAvatarKind.Ranger,
                "ranger" => PlayerAvatarKind.Ranger,
                "slime" => PlayerAvatarKind.Slime,
                _ => PlayerAvatarKind.Warrior,
            };
        }

        private static string QueryValue(string key)
        {
            string url = Application.absoluteURL;
            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0)
            {
                return string.Empty;
            }

            string[] pairs = url.Substring(queryIndex + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
            }
            return string.Empty;
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            try
            {
                return Uri.UnescapeDataString(value.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }
    }
}
