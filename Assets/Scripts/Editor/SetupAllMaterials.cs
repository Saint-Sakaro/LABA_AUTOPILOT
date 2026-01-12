#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

public class SetupAllMaterials
{
    [MenuItem("Tools/Настроить все материалы (камни + деревья)")]
    public static void SetupAllMaterialsOneClick()
    {
        Debug.Log("=== Начало настройки всех материалов ===");

        // 1. Находим URP Lit шейдер
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден URP Lit шейдер!", "OK");
            return;
        }

        // 2. Находим текстуры для камней
        string[] rockDiffuseGuids = AssetDatabase.FindAssets("rocky_terrain_02_diff_4k t:Texture2D");
        string[] rockNormalGuids = AssetDatabase.FindAssets("rocky_terrain_02_nor_gl_4k");
        string[] rockRoughGuids = AssetDatabase.FindAssets("rocky_terrain_02_rough_4k");

        if (rockDiffuseGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найдена текстура rocky_terrain_02_diff_4k!", "OK");
            return;
        }

        Texture2D rockDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(rockDiffuseGuids[0]));
        Texture2D rockNormal = rockNormalGuids.Length > 0 ? 
            AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(rockNormalGuids[0])) : null;
        Texture2D rockRough = rockRoughGuids.Length > 0 ? 
            AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(rockRoughGuids[0])) : null;

        // 3. Находим текстуры для деревьев
        string[] treeDiffuseGuids = AssetDatabase.FindAssets("diffuse t:Texture2D", new[] { "Assets/Tree_Textures" });
        string[] treeNormalGuids = AssetDatabase.FindAssets("normal_specular t:Texture2D", new[] { "Assets/Tree_Textures" });

        if (treeDiffuseGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найдена текстура diffuse в Assets/Tree_Textures!", "OK");
            return;
        }

        Texture2D treeDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(treeDiffuseGuids[0]));
        Texture2D treeNormal = treeNormalGuids.Length > 0 ? 
            AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(treeNormalGuids[0])) : null;

        // 4. Создаем/обновляем материал для камней
        Material rockMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RockMaterial.mat");
        if (rockMaterial == null)
        {
            rockMaterial = new Material(urpLitShader);
            rockMaterial.name = "RockMaterial";
            AssetDatabase.CreateAsset(rockMaterial, "Assets/Materials/RockMaterial.mat");
        }
        else
        {
            rockMaterial.shader = urpLitShader;
        }

        rockMaterial.SetTexture("_BaseMap", rockDiffuse);
        if (rockNormal != null)
        {
            rockMaterial.SetTexture("_BumpMap", rockNormal);
            rockMaterial.EnableKeyword("_NORMALMAP");
            rockMaterial.SetFloat("_BumpScale", 1.0f);
        }
        if (rockRough != null)
        {
            rockMaterial.SetTexture("_MetallicGlossMap", rockRough);
            rockMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        rockMaterial.SetFloat("_Smoothness", 0.5f);
        rockMaterial.SetFloat("_Metallic", 0.0f);
        EditorUtility.SetDirty(rockMaterial);

        // 5. Создаем/обновляем материалы для деревьев
        Material treeBarkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeBarkMaterial_URP.mat");
        if (treeBarkMaterial == null)
        {
            treeBarkMaterial = new Material(urpLitShader);
            treeBarkMaterial.name = "TreeBarkMaterial_URP";
            AssetDatabase.CreateAsset(treeBarkMaterial, "Assets/Materials/TreeBarkMaterial_URP.mat");
        }
        else
        {
            treeBarkMaterial.shader = urpLitShader;
        }

        treeBarkMaterial.SetTexture("_BaseMap", treeDiffuse);
        if (treeNormal != null)
        {
            treeBarkMaterial.SetTexture("_BumpMap", treeNormal);
            treeBarkMaterial.EnableKeyword("_NORMALMAP");
            treeBarkMaterial.SetFloat("_BumpScale", 1.0f);
        }
        treeBarkMaterial.SetFloat("_Smoothness", 0.3f);
        treeBarkMaterial.SetFloat("_Metallic", 0.0f);
        EditorUtility.SetDirty(treeBarkMaterial);

        Material treeLeafMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeLeafMaterial_URP.mat");
        if (treeLeafMaterial == null)
        {
            treeLeafMaterial = new Material(urpLitShader);
            treeLeafMaterial.name = "TreeLeafMaterial_URP";
            AssetDatabase.CreateAsset(treeLeafMaterial, "Assets/Materials/TreeLeafMaterial_URP.mat");
        }
        else
        {
            treeLeafMaterial.shader = urpLitShader;
        }

        treeLeafMaterial.SetTexture("_BaseMap", treeDiffuse);
        if (treeNormal != null)
        {
            treeLeafMaterial.SetTexture("_BumpMap", treeNormal);
            treeLeafMaterial.EnableKeyword("_NORMALMAP");
            treeLeafMaterial.SetFloat("_BumpScale", 1.0f);
        }
        treeLeafMaterial.SetFloat("_Smoothness", 0.2f);
        treeLeafMaterial.SetFloat("_Metallic", 0.0f);
        treeLeafMaterial.SetFloat("_Cutoff", 0.3f);
        treeLeafMaterial.EnableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(treeLeafMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. Применяем материалы к префабам камней
        int rocksUpdated = 0;
        for (int i = 1; i <= 16; i++)
        {
            string prefabPath = $"Assets/Prefabs/Rocks/Rock_{i}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
                MeshRenderer renderer = prefabInstance.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = rockMaterial;
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    rocksUpdated++;
                }
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }

        // 7. Применяем материалы к префабу дерева
        int treesUpdated = 0;
        string treePrefabPath = "Assets/Tree.prefab";
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabPath);
        if (treePrefab != null)
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
                treesUpdated++;
            }
            PrefabUtility.UnloadPrefabContents(prefabInstance);
        }

        // 8. Находим PlatformGenerator в сцене и назначаем материалы (если нужно)
        PlatformGenerator[] generators = Object.FindObjectsOfType<PlatformGenerator>();
        int generatorsUpdated = 0;
        
        foreach (PlatformGenerator generator in generators)
        {
            SerializedObject so = new SerializedObject(generator);
            
            // Назначаем материал для камней через рефлексию или SerializedProperty
            // К сожалению, PlatformGenerator не имеет SerializeField для материалов камней/деревьев
            // Но мы можем обновить префабы, и они будут использоваться при создании
            
            generatorsUpdated++;
        }

        Debug.Log("=== Завершено настройка всех материалов ===");
        Debug.Log($"Материал камней: {rockMaterial.name}");
        Debug.Log($"Материал коры: {treeBarkMaterial.name}");
        Debug.Log($"Материал листьев: {treeLeafMaterial.name}");
        Debug.Log($"Префабов камней обновлено: {rocksUpdated}");
        Debug.Log($"Префабов деревьев обновлено: {treesUpdated}");

        EditorUtility.DisplayDialog("Успех!", 
            $"Все материалы настроены!\n\n" +
            $"✅ Материал камней создан/обновлен\n" +
            $"✅ Материалы дерева созданы/обновлены\n" +
            $"✅ Префабов камней обновлено: {rocksUpdated}\n" +
            $"✅ Префабов деревьев обновлено: {treesUpdated}\n\n" +
            $"Теперь:\n" +
            $"1. Удалите все объекты из сцены (Tools → Удалить все деревья и камни)\n" +
            $"2. Нажмите Play - новые объекты будут с правильными материалами!",
            "OK");
    }
}
#endif
