#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlatformGenerator))]
public class PlatformGeneratorSetup : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlatformGenerator generator = (PlatformGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Автоматическая настройка", EditorStyles.boldLabel);

        if (GUILayout.Button("Автоматически заполнить Tree Prefabs"))
        {
            AutoFillTreePrefabs(generator);
        }

        if (GUILayout.Button("Автоматически заполнить Rock Prefabs"))
        {
            AutoFillRockPrefabs(generator);
        }

        EditorGUILayout.Space();
        
        if (GUILayout.Button("Применить материалы к префабам камней"))
        {
            ApplyMaterialsToRocks.ApplyRockMaterials();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Исправить материалы дерева для URP"))
        {
            FixTreeMaterials.FixTreeMaterialsForURP();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Если массивы пустые, используйте кнопки выше для автоматического заполнения. " +
            "Для деревьев будет использован найденный префаб Tree.prefab. " +
            "Для камней: используйте кнопку 'Применить материалы' чтобы применить текстуры rocky_terrain к префабам.",
            MessageType.Info
        );
    }

    private void AutoFillTreePrefabs(PlatformGenerator generator)
    {
        SerializedProperty treePrefabsProp = serializedObject.FindProperty("treePrefabs");
        
        
        string[] guids = AssetDatabase.FindAssets("Tree t:Prefab");
        GameObject defaultTree = null;
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            defaultTree = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        if (defaultTree == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден префаб Tree.prefab! Убедитесь, что он существует в проекте.", "OK");
            return;
        }

        
        int filled = 0;
        for (int i = 0; i < treePrefabsProp.arraySize; i++)
        {
            SerializedProperty element = treePrefabsProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
            {
                element.objectReferenceValue = defaultTree;
                filled++;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.DisplayDialog("Успех", $"Заполнено {filled} слотов префабами деревьев.", "OK");
    }

    private void AutoFillRockPrefabs(PlatformGenerator generator)
    {
        SerializedProperty rockPrefabsProp = serializedObject.FindProperty("rockPrefabs");
        
        
        string[] guids = AssetDatabase.FindAssets("rock t:Prefab");
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("stone t:Prefab");
        }

        if (guids.Length == 0)
        {
            
            if (EditorUtility.DisplayDialog("Префабы камней не найдены", 
                "Не найдены префабы камней. Хотите создать простые префабы камней из примитивов Unity?", 
                "Да, создать", "Отмена"))
            {
                CreateSimpleRockPrefabs(rockPrefabsProp);
            }
            return;
        }

        
        int filled = 0;
        int guidIndex = 0;
        for (int i = 0; i < rockPrefabsProp.arraySize && guidIndex < guids.Length; i++)
        {
            SerializedProperty element = rockPrefabsProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (rockPrefab != null)
                {
                    element.objectReferenceValue = rockPrefab;
                    filled++;
                }
                guidIndex++;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.DisplayDialog("Успех", 
            $"Заполнено {filled} слотов префабами камней. Всего найдено {guids.Length} префабов.", 
            "OK");
    }

    private void CreateSimpleRockPrefabs(SerializedProperty rockPrefabsProp)
    {
        
        string prefabFolder = "Assets/Prefabs/Rocks";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Rocks");
        }

        int created = 0;
        for (int i = 0; i < rockPrefabsProp.arraySize && created < 16; i++)
        {
            SerializedProperty element = rockPrefabsProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == null)
            {
                
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{i + 1}";
                
                
                float scale = UnityEngine.Random.Range(0.5f, 2f);
                rock.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
                
                
                string prefabPath = $"{prefabFolder}/{rock.name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rock, prefabPath);
                
                element.objectReferenceValue = prefab;
                created++;
                
                
                DestroyImmediate(rock);
            }
        }

        serializedObject.ApplyModifiedProperties();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Успех", 
            $"Создано {created} простых префабов камней в {prefabFolder}. Вы можете заменить их на более детализированные модели позже.", 
            "OK");
    }
}
#endif
