#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class SetupPlatformGenerator
{
    [MenuItem("Tools/Настроить PlatformGenerator (деревья + камни)")]
    public static void Setup()
    {
        
        string prefabPath = "Assets/Prefabs/v002/PlatformGenerator.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден префаб PlatformGenerator в Assets/Prefabs/v002/!", "OK");
            return;
        }

        
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tree.prefab");
        if (treePrefab == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден префаб Tree.prefab в Assets/!", "OK");
            return;
        }

        
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
        PlatformGenerator generator = prefabInstance.GetComponent<PlatformGenerator>();
        
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден компонент PlatformGenerator в префабе!", "OK");
            PrefabUtility.UnloadPrefabContents(prefabInstance);
            return;
        }

        
        SerializedObject so = new SerializedObject(generator);
        SerializedProperty treePrefabsProp = so.FindProperty("treePrefabs");
        
        if (treePrefabsProp != null)
        {
            treePrefabsProp.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                treePrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = treePrefab;
            }
        }

        
        string rocksFolderPath = "Assets/Prefabs/Rocks";
        if (!AssetDatabase.IsValidFolder(rocksFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Rocks");
        }

        GameObject[] rockPrefabs = new GameObject[16];
        bool needToCreateRocks = false;

        for (int i = 1; i <= 16; i++)
        {
            string rockPath = $"{rocksFolderPath}/Rock_{i}.prefab";
            GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
            
            if (rockPrefab == null)
            {
                needToCreateRocks = true;
                
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{i}";
                
                
                float scale = UnityEngine.Random.Range(0.5f, 1.5f);
                rock.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
                
                
                rockPrefab = PrefabUtility.SaveAsPrefabAsset(rock, rockPath);
                Object.DestroyImmediate(rock);
                
                Debug.Log($"Создан префаб камня: {rockPath}");
            }
            
            rockPrefabs[i - 1] = rockPrefab;
        }

        
        SerializedProperty rockPrefabsProp = so.FindProperty("rockPrefabs");
        if (rockPrefabsProp != null)
        {
            rockPrefabsProp.arraySize = 16;
            for (int i = 0; i < 16; i++)
            {
                rockPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = rockPrefabs[i];
            }
        }

        
        so.ApplyModifiedProperties();
        PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabInstance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = "PlatformGenerator настроен!\n\n";
        message += "✅ Массив деревьев заполнен (Tree.prefab)\n";
        if (needToCreateRocks)
        {
            message += "✅ Создано 16 префабов камней в Assets/Prefabs/Rocks/\n";
        }
        else
        {
            message += "✅ Массив камней заполнен из существующих префабов\n";
        }
        message += "\nТеперь можно использовать PlatformGenerator в сцене!";

        EditorUtility.DisplayDialog("Успех!", message, "OK");
    }
}
#endif
