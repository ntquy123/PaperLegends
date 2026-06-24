using UnityEngine;

public class DayNightWeatherManager : MonoBehaviour
{
    public static DayNightWeatherManager Instance;

    [Header("Skybox Materials")]
    public Material morningSkybox;
    public Material afternoonSkybox;
    public Material eveningSkybox;
    public Material RainSkybox;
    private void Awake()
    {
        Instance = this;
    }

    // public void RandomizeEnvironment()
    // {
    //     TimeOfDay time = (TimeOfDay)Random.Range(0, 3);
    //     //WeatherType weather = Random.value < 0.1f ? WeatherType.Rainy : WeatherType.Sunny;  // Tỷ lệ mưa 20%
    //     WeatherType weather = WeatherType.Sunny;
    //     //WeatherType weather = WeatherType.Rainy;
    //     ApplyEnvironment(time, weather);
    // }

    public void ApplyEnvironment(TimeOfDay time, WeatherType weather)
    {
        GameSessionClientLocal sessionClientLocal = GameSessionClientLocal.Instance;
        Light directionalLight = sessionClientLocal != null ? sessionClientLocal.directionalLight : null;

        if (directionalLight == null)
        {
            Debug.LogWarning("DayNightWeatherManager: directionalLight is missing on GameSessionClientLocal; skipping lighting changes.");
        }

        GameObject rainEffect = sessionClientLocal != null ? sessionClientLocal.rainEffect : null;
        GameObject poolWaterEffect = sessionClientLocal != null ? sessionClientLocal.poolWaterEffect : null;

        // Điều chỉnh ánh sáng và skybox cho thời gian trong ngày
        switch (time)
        {
            case TimeOfDay.Morning:
                if (directionalLight != null)
                {
                    directionalLight.intensity = Random.Range(1.1f, 1.4f);

                    // Vàng nhạt – nhẹ nhàng cho buổi sáng
                    directionalLight.color = new Color(
                        Random.Range(0.95f, 1.0f),  // Red
                        Random.Range(0.9f, 1.0f),  // Green
                        Random.Range(0.7f, 0.8f)   // Blue
                    );
                }

                if (morningSkybox != null)
                    RenderSettings.skybox = morningSkybox;

                PlayTimeClip(SoundManager.Instance?.morningClip);
                break;

            case TimeOfDay.Afternoon:
                if (directionalLight != null)
                {
                    directionalLight.intensity = Random.Range(0.9f, 1.3f);

                    // Vàng tươi thay vì cam/đỏ
                    directionalLight.color = new Color(
                        Random.Range(0.95f, 1.0f),  // Red
                        Random.Range(0.85f, 0.95f),// Green
                        Random.Range(0.65f, 0.75f) // Blue
                    );
                }

                if (afternoonSkybox != null)
                    RenderSettings.skybox = afternoonSkybox;

                PlayTimeClip(SoundManager.Instance?.afternoonClip);
                break;

            case TimeOfDay.Evening:
                if (directionalLight != null)
                {
                    directionalLight.intensity = Random.Range(0.6f, 0.9f);

                    // Vàng nhạt – hạ cường độ đỏ, giữ màu dịu
                    directionalLight.color = new Color(
                        Random.Range(0.85f, 0.95f),  // Red
                        Random.Range(0.8f, 0.9f),    // Green
                        Random.Range(0.6f, 0.75f)    // Blue
                    );
                }

                if (eveningSkybox != null)
                    RenderSettings.skybox = eveningSkybox;

                PlayTimeClip(SoundManager.Instance?.eveningClip);
                break;
        }


        // Kiểm tra thời tiết (mưa)
        if (weather == WeatherType.Rainy)
        {
            if (RainSkybox != null)
                RenderSettings.skybox = RainSkybox;
            if (rainEffect != null)
                rainEffect.SetActive(true);
            if (poolWaterEffect != null)
                poolWaterEffect.SetActive(true);
            PlayRainClip();
        }
        else
        {
            if (rainEffect != null)
                rainEffect.SetActive(false);
            if (poolWaterEffect != null)
                poolWaterEffect.SetActive(false);
            //if (bgmSource != null && rainClip != null)
            //{
            //    bgmSource.loop = false;
            //    bgmSource.Stop();
            //}
        }
    }

    private void PlayTimeClip(AudioClip clip)
    {
        if (clip == null)
            return;

        SoundManager.Instance?.OverrideBgm(clip, 1.0f, true);
    }

    private void PlayRainClip()
    {
        AudioClip clip = SoundManager.Instance?.rainClip;

        if (clip == null)
            return;

        SoundManager.Instance?.OverrideBgm(clip, 1.0f, true);
    }

    public void StopEnvironmentSound()
    {
        SoundManager.Instance?.ClearBgmOverride();
        GameSessionClientLocal sessionClientLocal = GameSessionClientLocal.Instance;
        GameObject rainEffect = sessionClientLocal != null ? sessionClientLocal.rainEffect : null;
        GameObject poolWaterEffect = sessionClientLocal != null ? sessionClientLocal.poolWaterEffect : null;
        if (rainEffect != null)
            rainEffect.SetActive(false);
        if (poolWaterEffect != null)
            poolWaterEffect.SetActive(false);
    }
}
