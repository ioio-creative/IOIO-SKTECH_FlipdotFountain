using System;
using System.IO;
using UnityEngine;

public class AudioConfig : MonoBehaviour
{
    [SerializeField] private string fountainConfigFileName;
    [SerializeField] private AudioSource[] tracks;

    private void Start()
    {
        string jsonFilePath = Path.Combine(Application.streamingAssetsPath, fountainConfigFileName);
        string jsonString = File.ReadAllText(jsonFilePath);
        int[] trackSettings = FountainConfig.FromJson(jsonString).audioTracks;
        for (int i = 0; i < tracks.Length; i++)
        {
            try
            {
                tracks[i].mute = trackSettings[i] == 0;
            }
            catch { }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            try
            {
                tracks[0].mute = !tracks[0].mute;
            }
            catch { }
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            try
            {
                tracks[1].mute = !tracks[1].mute;
            }
            catch { }
        }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            try
            {
                tracks[2].mute = !tracks[2].mute;
            }
            catch { }
        }
    }
}

[Serializable]
public struct FountainConfig
{
    public int[] audioTracks;

    public static FountainConfig FromJson(string jsonData)
    {
        return JsonUtility.FromJson<FountainConfig>(jsonData);
    }
}

