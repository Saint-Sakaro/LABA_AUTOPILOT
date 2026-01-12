#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FixPinkMaterials
{
    [MenuItem("Tools/Исправить розовые материалы (полная проверка)")]
    public static void FixAllPinkMaterials()
    {
        Debug.Log("=== Начало исправления розовых материалов ===");

        // Находим правильный URP Lit шейдер
        Shader urpLitShader = null;
        
        // Пробуем разные варианты имени шейдера
        string[] shaderNames = new string[]
        {
            "Universal Render Pipeline/Lit",
            "Shader Graphs/Universal Render Pipeline/Lit",
            "URP/Lit",
            "Universal Render Pipeline/Lit (Forward)",
            "Universal Render Pipeline/Lit (Deferred)"
        };

        foreach (string shaderName in shaderNames)
        {
            urpLitShader = Shader.Find(shaderName);
            if (urpLitShader != null)
            {
                Debug.Log($"Найден шейдер: {shaderName}");
                break;
            }
        }

        if (urpLitShader == null)
        {
            // Пробуем найти через AssetDatabase
            string[] shaderGuids = AssetDatabase.FindAssets("Lit t:Shader");
            foreach (string guid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null && (shader.name.Contains("Universal") || shader.name.Contains("URP")))
                {
                    urpLitShader = shader;
                    Debug.Log($"Найден шейдер через AssetDatabase: {shader.name} ({path})");
                    break;
                }
            }
        }

        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("КРИТИЧЕСКАЯ ОШИБКА", 
                "Не найден URP Lit шейдер!\n\n" +
                "Убедитесь, что:\n" +
                "1. Universal Render Pipeline установлен в Package Manager\n" +
                "2. Проект использует URP (не Built-in или HDRP)\n" +
                "3. Шейдеры URP доступны в проекте",
                "OK");
            return;
        }

        Debug.Log($"Используется шейдер: {urpLitShader.name}");

        // Находим материалы
        Material rockMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RockMaterial.mat");
        Material treeBarkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeBarkMaterial_URP.mat");
        Material treeLeafMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeLeafMaterial_URP.mat");

        // Исправляем материалы, если они используют неправильный шейдер
        if (rockMaterial != null)
        {
            if (rockMaterial.shader != urpLitShader)
            {
                Debug.Log($"Исправляю шейдер в RockMaterial: {rockMaterial.shader.name} -> {urpLitShader.name}");
                rockMaterial.shader = urpLitShader;
                EditorUtility.SetDirty(rockMaterial);
            }
        }

        if (treeBarkMaterial != null)
        {
            if (treeBarkMaterial.shader != urpLitShader)
            {
                Debug.Log($"Исправляю шейдер в TreeBarkMaterial_URP: {treeBarkMaterial.shader.name} -> {urpLitShader.name}");
                treeBarkMaterial.shader = urpLitShader;
                EditorUtility.SetDirty(treeBarkMaterial);
            }
        }

        if (treeLeafMaterial != null)
        {
            if (treeLeafMaterial.shader != urpLitShader)
            {
                Debug.Log($"Исправляю шейдер в TreeLeafMaterial_URP: {treeLeafMaterial.shader.name} -> {urpLitShader.name}");
                treeLeafMaterial.shader = urpLitShader;
                EditorUtility.SetDirty(treeLeafMaterial);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Обновляем все префабы камней
        int rocksFixed = 0;
        for (int i = 1; i <= 16; i++)
        {
            string prefabPath = $"Assets/Prefabs/Rocks/Rock_{i}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab != null && rockMaterial != null)
            {
                GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
                MeshRenderer renderer = prefabInstance.GetComponent<MeshRenderer>();
                
                if (renderer != null)
                {
                    renderer.material = rockMaterial;
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    rocksFixed++;
                    Debug.Log($"Обновлен префаб: {prefabPath}");
                }
                
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }

        // Обновляем префаб дерева
        int treesFixed = 0;
        string treePrefabPath = "Assets/Tree.prefab";
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabPath);
        
        if (treePrefab != null && treeBarkMaterial != null && treeLeafMaterial != null)
        {
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(treePrefabPath);
            MeshRenderer renderer = prefabInstance.GetComponent<MeshRenderer>();
            
            if (renderer != null)
            {
                Material[] materials = new Material[2];
                materials[0] = treeBarkMaterial;
                materials[1] = treeLeafMaterial;
                renderer.materials = materials;
                PrefabUtility.SaveAsPrefabAsset(prefabInstance, treePrefabPath);
                treesFixed++;
                Debug.Log($"Обновлен префаб дерева: {treePrefabPath}");
            }
            
            PrefabUtility.UnloadPrefabContents(prefabInstance);
        }

        // Обновляем объекты в сцене
        int sceneRocksFixed = 0;
        int sceneTreesFixed = 0;
        
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null) continue;

            // Проверяем, розовый ли материал (материал с отсутствующим шейдером)
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                if (obj.name.StartsWith("Rock_") || obj.name.Contains("Rock"))
                {
                    if (rockMaterial != null)
                    {
                        renderer.material = rockMaterial;
                        sceneRocksFixed++;
                    }
                }
                else if (obj.name.Contains("Tree") || obj.name.Contains("tree"))
                {
                    if (treeBarkMaterial != null && treeLeafMaterial != null)
                    {
                        Material[] materials = new Material[2];
                        materials[0] = treeBarkMaterial;
                        materials[1] = treeLeafMaterial;
                        renderer.materials = materials;
                        sceneTreesFixed++;
                    }
                }
            }
        }

        Debug.Log("=== Завершено исправление материалов ===");
        Debug.Log($"Префабов камней обновлено: {rocksFixed}");
        Debug.Log($"Префабов деревьев обновлено: {treesFixed}");
        Debug.Log($"Объектов камней в сцене исправлено: {sceneRocksFixed}");
        Debug.Log($"Объектов деревьев в сцене исправлено: {sceneTreesFixed}");

        EditorUtility.DisplayDialog("Завершено", 
            $"Исправление материалов завершено!\n\n" +
            $"Префабов камней: {rocksFixed}\n" +
            $"Префабов деревьев: {treesFixed}\n" +
            $"Объектов в сцене (камни): {sceneRocksFixed}\n" +
            $"Объектов в сцене (деревья): {sceneTreesFixed}\n\n" +
            $"Проверьте консоль для подробностей.\n\n" +
            $"Если объекты все еще розовые:\n" +
            $"1. Удалите все объекты из сцены\n" +
            $"2. Перезапустите игру (Play)",
            "OK");
    }
}
#endif
