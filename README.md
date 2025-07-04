# Build Excluder Tool for Unity 2021.3

A full-featured editor tool for conditionally including folders and files in a Unity build based on specified define symbols.

## 📦 Installation

### Unity Package Manager (UPM)

You can install Build Excluder directly from GitHub using the Unity Package Manager:

1. Open your Unity project.
2. Go to **Window → Package Manager**.
3. Click the **+** button (top left) and select **Add package from Git URL...**
4. Enter:

   ```
   https://github.com/Yansubaev/BuildExcluder.git
   ```

5. Click **Add**. The package will appear in your Packages list as `Build Excluder`.

For more details, see the [official Unity documentation](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

## 🎯 Main Features

- **Conditional asset inclusion** in the build based on define symbols
- **Automatic restoration** of excluded files after the build
- **Define symbol support** with `DEFINE` and `!DEFINE` logic
- **Convenient UI** for managing inclusion rules
- **Protection against data loss** in case of build failures
- **Cross-platform** (Windows, macOS, Linux)

## 📁 Structure

```
Assets/
├── BuildExcluder/
│   └── Editor/
│       ├── BuildExcludeConfig.cs      # Config structure
│       ├── BuildExcludeConfig.json    # Config file
│       ├── BuildExcluderProcessor.cs  # Pre/post build logic
│       ├── BuildExcluderWindow.cs     # UI window
│       └── BuildExcluderInitializer.cs # Restore on startup
|
ExcludedAssets/                        # Temporary storage for excluded assets
```

## 🚀 How to Use

### 1. Open the Tool
- In Unity, go to `Window → Build Excluder`

### 2. Set Up Exclusion Rules
- In the opened window, click `Create/Reload Config` to create a default config
- Select an asset in the Project window
- Click `Define Constraints` and then `+` to add the first inclusion rule
- For existing rules, click `+` to add additional conditions
- Specify a define symbol (e.g., `STORE_GOOGLEPLAY` or `!DEBUG_BUILD`)

### 3. Manage Define Constraints
- Each asset can have multiple define conditions
- You can remove individual defines with the `-` button
- The final status shows whether the asset will be included or excluded

### 4. Manage Define Symbols
- Go to `Player Settings → Other Settings → Scripting Define Symbols`
- Add the required define symbols (e.g., `STORE_GOOGLEPLAY`, `STORE_APPGALLERY`)

### 5. Build the Project
- During the build, the tool will automatically:
  - Exclude assets that don't meet their inclusion criteria
  - Restore them after the build is complete

## ⚙️ Define Symbol Logic

### Inclusion Rules:
- `DEFINE` - include if the define **IS** set (exclude if NOT set)
- `!DEFINE` - include if the define is **NOT** set (exclude if IS set)

### Examples of Multiple Conditions:
```json
{
  "assetPath": "Assets/DeveloperAssets",
  "defines": ["!DEVELOPMENT_BUILD", "!DEBUG_BUILD"]
}
```
**Result:** The DeveloperAssets folder will be included if **any** of the conditions are met (`DEVELOPMENT_BUILD` is NOT set OR `DEBUG_BUILD` is NOT set). It will be excluded only if both `DEVELOPMENT_BUILD` AND `DEBUG_BUILD` are set.

```json
{
  "assetPath": "Assets/StoreSpecific/GooglePlay",
  "defines": ["STORE_GOOGLEPLAY", "!EXCLUDE_GOOGLE"]
}
```
**Result:** The GooglePlay folder will be included if `STORE_GOOGLEPLAY` is set OR `EXCLUDE_GOOGLE` is NOT set. It will be excluded only if `STORE_GOOGLEPLAY` is NOT set AND `EXCLUDE_GOOGLE` is set.

## 🛡️ Safety System

### Automatic Restoration:
- On Unity startup, the `ExcludedAssets/` folder is checked
- If any "forgotten" files are found, they are automatically restored
- All operations are logged in the Console

### Collision Protection:
- Checks for file existence before moving
- Warnings about name conflicts
- State is saved in SessionState

## 📋 Default Configuration

```json
{
  "entries": [
    {
      "assetPath": "Assets/StoreSpecific/GooglePlay",
      "defines": ["STORE_GOOGLEPLAY"]
    },
    {
      "assetPath": "Assets/StoreSpecific/AppGallery", 
      "defines": ["STORE_APPGALLERY"]
    },
    {
      "assetPath": "Assets/DebugTools",
      "defines": ["!DEBUG_BUILD"]
    },
    {
      "assetPath": "Assets/DeveloperAssets",
      "defines": ["!DEVELOPMENT_BUILD"]
    }
  ]
}
```

### UI Features:

### Main Window:
- **List of all rules** with detailed status for each define
- **Selected asset info** in the Project window with Define Constraints
- **Current project define symbols**

### Rule Management:
- **Multiple defines** for a single asset (like in .asmdef files)
- **Add new defines** to existing rules
- **Remove individual defines** from define constraints list
- **Final status** (INCLUDED/EXCLUDED) based on all rules

## 🚨 Troubleshooting

### If assets are not restored:
1. Check the `ExcludedAssets/` folder in the project root
2. Click `Restore All Assets` in the Build Excluder window
3. Check the Console for errors

### If rules do not work:
1. Make sure define symbols are correctly set in Player Settings
2. Check the syntax in `BuildExcludeConfig.json`
3. Reload the config via `Create/Reload Config`

## 📦 System Requirements

- Unity 2021.3 or higher
- Windows, macOS, or Linux