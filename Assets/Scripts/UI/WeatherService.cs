using System;
using System.Collections;
using TerrariumDays.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Fetches the current weather at the player's location from Open-Meteo (free, no API key)
    /// every half hour. The only network use in the app: it is optional, the rest of the
    /// game works offline, and when location or network is unavailable the last result
    /// (or a short notice) is shown instead. Coordinates are rounded to ~1 km before sending.
    /// </summary>
    public sealed class WeatherService
    {
        public const float RefreshSeconds = 30f * 60f;
        private const float LocationTimeoutSeconds = 20f;
        private const int RequestTimeoutSeconds = 15;
        private const string CacheKey = "weather.lastText";

        public const string LocationOffText = "位置情報がオフのため天気を表示できません";
        public const string LocationFailedText = "現在地を取得できません";
        public const string OfflineText = "天気を取得できません（オフライン）";
        public const string LoadingText = "天気を取得中…";

        public IEnumerator Run(Action<string> onText)
        {
            onText(Cached() ?? LoadingText);
            while (true)
            {
                yield return FetchOnce(onText);
                yield return new WaitForSecondsRealtime(RefreshSeconds);
            }
        }

        private IEnumerator FetchOnce(Action<string> onText)
        {
            if (!Input.location.isEnabledByUser)
            {
                onText(Cached() ?? LocationOffText);
                yield break;
            }

            Input.location.Start(5000f, 5000f);
            var waited = 0f;
            while (Input.location.status == LocationServiceStatus.Initializing && waited < LocationTimeoutSeconds)
            {
                yield return new WaitForSecondsRealtime(1f);
                waited += 1f;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Stop();
                onText(Cached() ?? LocationFailedText);
                yield break;
            }

            var location = Input.location.lastData;
            Input.location.Stop();

            using (var request = UnityWebRequest.Get(OpenMeteo.CurrentWeatherUrl(location.latitude, location.longitude)))
            {
                request.timeout = RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success
                    && OpenMeteo.TryParse(request.downloadHandler.text, out var report))
                {
                    var text = report.ToDisplayText();
                    PlayerPrefs.SetString(CacheKey, text);
                    PlayerPrefs.Save();
                    onText(text);
                }
                else
                {
                    var cached = Cached();
                    onText(cached != null ? cached + "（前回）" : OfflineText);
                }
            }
        }

        private static string Cached()
        {
            var text = PlayerPrefs.GetString(CacheKey, string.Empty);
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
