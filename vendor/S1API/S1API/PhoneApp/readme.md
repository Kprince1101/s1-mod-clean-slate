# 📱 S1API.PhoneApp

The `PhoneApp` module allows developers to create fully custom in-game phone applications for Schedule I using a clean and reusable API.  
This module handles UI injection, icon management, and IL2CPP-safe app creation automatically.


---

## 🚀 Quick Start Guide

### 1. Create your own App by inheriting `PhoneApp`

```csharp
using UnityEngine;
using UnityEngine.UI;
using S1API.PhoneApp;

public class MyAwesomeApp : PhoneApp
{
    protected override string AppName => "MyAwesomeApp";
    protected override string AppTitle => "My Awesome App";
    protected override string IconLabel => "Awesome";
    protected override string IconFileName => "my_icon.png";

    protected override void OnCreated(GameObject container)
    {
        GameObject panel = UIFactory.Panel("MainPanel", container.transform, Color.black);
        UIFactory.Text("HelloText", "📱 Hello from MyAwesomeApp!", panel.transform, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
    }
}
```

### 2. Register your app in `PhoneAppManager`
```csharp
using MelonLoader;
using S1API.PhoneApp;

public class MyMod : MelonMod
{
    public override void OnApplicationStart()
    {
        PhoneAppManager.Register(new MyAwesomeApp());
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "MainScene") // Replace with your in-game scene
        {
            PhoneAppManager.InitAll(LoggerInstance);
        }
    }
}
```

### 3. Add your icon to the mod folder

- Save your icon in: UserData/my_icon.png

- Recommended size: 128x128 or 256x256 PNG

- Transparent background preferred

### Check the MyAwesomeApp.cs file as an example 