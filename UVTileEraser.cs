/*

Unity script to erase UV tiles from meshes based on user-defined grid
Based on NDMF framework (credit to nadena.dev), so NDMF is a dependency which must be installed
Made by SorrowfulExistence for Wosted according to her specifications

*/



using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using System.Linq;

[assembly: ExportsPlugin(typeof(UVTileEraserTool.UVTileEraserPlugin))]

namespace UVTileEraserTool
{
    [System.Serializable]
    public class UVTileGrid
    {
        public bool[] tiles = new bool[16]; //4x4 grid for standard mode
        public Dictionary<Vector2Int, bool> customTiles = new Dictionary<Vector2Int, bool>(); //for advanced mode
    }
    
    [AddComponentMenu("UV Tile Eraser")]
    public class UVTileEraser : MonoBehaviour
    {
        [Header("Target Meshes")]
        public List<SkinnedMeshRenderer> targetMeshes = new List<SkinnedMeshRenderer>();
        
        [Header("UV Channel")]
        [Range(0, 7)]
        [Tooltip("Unity supports UV0 through UV7")]
        public int uvChannel = 0;
        
        [Header("Grid Mode")]
        [Tooltip("Standard: 0-3 grid, Advanced: Custom range")]
        public bool useAdvancedMode = false;
        
        [Header("Advanced Grid Settings")]
        [Range(-64, 64)]
        public int minU = -4;
        [Range(-64, 64)]
        public int maxU = 4;
        [Range(-64, 64)]
        public int minV = -4;
        [Range(-64, 64)]
        public int maxV = 4;
        
        [Header("Tiles to Erase")]
        public UVTileGrid tilesToErase = new UVTileGrid();
        
        [Header("Erase Mode")]
        [Tooltip("Any Vertex: Erase if ANY vertex of triangle is in tile\nAll Vertices: Erase only if ALL vertices are in tile")]
        public bool eraseIfAnyVertex = true;
        
        [Header("Material Filter (Optional)")]
        [Tooltip("If set, only faces using these materials will be erased. Leave empty to erase all materials.")]
        public List<Material> materialFilter = new List<Material>();
        
        [Tooltip("Filter mode: Include = only erase these materials, Exclude = erase all except these materials")]
        public bool includeMaterials = true;
    }
    
    public class UVTileEraserPlugin : Plugin<UVTileEraserPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("UV Tile Eraser", ctx =>
                {
                    var erasers = ctx.AvatarRootObject.GetComponentsInChildren<UVTileEraser>();
                    
                    foreach (var eraser in erasers)
                    {
                        if (eraser.enabled)
                        {
                            ProcessEraser(eraser);
                        }
                    }
                });
        }
        
        private void ProcessEraser(UVTileEraser eraser)
        {
            if (eraser.targetMeshes == null || eraser.targetMeshes.Count == 0)
            {
                Debug.LogWarning($"[UV Tile Eraser] No target meshes on {eraser.gameObject.name}");
                return;
            }
            
            var totalErased = 0;
            
            foreach (var renderer in eraser.targetMeshes)
            {
                if (renderer == null || renderer.sharedMesh == null)
                    continue;
                
                var erased = ProcessMesh(renderer, eraser);
                totalErased += erased;
            }
            
            Debug.Log($"[UV Tile Eraser] Erased {totalErased} faces total");
        }
        
        private int ProcessMesh(SkinnedMeshRenderer renderer, UVTileEraser eraser)
        {
            var mesh = Object.Instantiate(renderer.sharedMesh);
            mesh.name = renderer.sharedMesh.name + "_UVErased";
            
            var uvs = new List<Vector2>();
            mesh.GetUVs(eraser.uvChannel, uvs);
            
            if (uvs.Count == 0)
            {
                Debug.LogWarning($"[UV Tile Eraser] No UVs in channel {eraser.uvChannel} for {renderer.name}");
                return 0;
            }
            
            var erasedCount = 0;
            var newSubmeshTriangles = new List<List<int>>();
            
            //get materials for filtering
            var materials = renderer.sharedMaterials;
            
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                //check material filter
                bool shouldProcessSubmesh = true;
                if (eraser.materialFilter != null && eraser.materialFilter.Count > 0 && materials != null && submesh < materials.Length)
                {
                    var material = materials[submesh];
                    bool isInFilter = eraser.materialFilter.Contains(material);
                    
                    if (eraser.includeMaterials)
                    {
                        //include mode: only process if material is in the filter
                        shouldProcessSubmesh = isInFilter;
                    }
                    else
                    {
                        //exclude mode: only process if material is NOT in the filter
                        shouldProcessSubmesh = !isInFilter;
                    }
                }
                
                var triangles = mesh.GetTriangles(submesh);
                var newTriangles = new List<int>();
                
                if (!shouldProcessSubmesh)
                {
                    //keep all triangles for this submesh
                    newTriangles.AddRange(triangles);
                }
                else
                {
                    //process triangles for erasure
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var v0 = triangles[i];
                        var v1 = triangles[i + 1];
                        var v2 = triangles[i + 2];
                        
                        var uv0 = uvs[v0];
                        var uv1 = uvs[v1];
                        var uv2 = uvs[v2];
                        
                        bool shouldErase;
                        
                        if (eraser.eraseIfAnyVertex)
                        {
                            shouldErase = IsInErasedTile(uv0, eraser) ||
                                         IsInErasedTile(uv1, eraser) ||
                                         IsInErasedTile(uv2, eraser);
                        }
                        else
                        {
                            shouldErase = IsInErasedTile(uv0, eraser) &&
                                         IsInErasedTile(uv1, eraser) &&
                                         IsInErasedTile(uv2, eraser);
                        }
                        
                        if (!shouldErase)
                        {
                            newTriangles.Add(v0);
                            newTriangles.Add(v1);
                            newTriangles.Add(v2);
                        }
                        else
                        {
                            erasedCount++;
                        }
                    }
                }
                
                newSubmeshTriangles.Add(newTriangles);
            }
            
            //apply new triangles
            for (int i = 0; i < newSubmeshTriangles.Count; i++)
            {
                mesh.SetTriangles(newSubmeshTriangles[i], i);
            }
            
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            
            renderer.sharedMesh = mesh;
            
            if (eraser.materialFilter != null && eraser.materialFilter.Count > 0)
            {
                Debug.Log($"[UV Tile Eraser] Erased {erasedCount} faces from {renderer.name} (material filter active)");
            }
            else
            {
                Debug.Log($"[UV Tile Eraser] Erased {erasedCount} faces from {renderer.name}");
            }
            return erasedCount;
        }
        
        private bool IsInErasedTile(Vector2 uv, UVTileEraser eraser)
        {
            //get tile coordinates
            int tileU = Mathf.FloorToInt(uv.x);
            int tileV = Mathf.FloorToInt(uv.y);
            
            if (eraser.useAdvancedMode)
            {
                //advanced mode: check custom tiles dictionary
                var tileKey = new Vector2Int(tileU, tileV);
                return eraser.tilesToErase.customTiles.ContainsKey(tileKey) && eraser.tilesToErase.customTiles[tileKey];
            }
            else
            {
                //standard mode: use 4x4 grid
                if (tileU < 0 || tileU > 3 || tileV < 0 || tileV > 3)
                    return false;
                
                int index = tileV * 4 + tileU;
                return eraser.tilesToErase.tiles[index];
            }
        }
    }
    
    [CustomEditor(typeof(UVTileEraser))]
    public class UVTileEraserEditor : Editor
    {
        private readonly string[] uvChannelNames = { "UV0", "UV1", "UV2", "UV3", "UV4", "UV5", "UV6", "UV7" };
        private Vector2 scrollPos;
        
        public override void OnInspectorGUI()
        {
            var eraser = (UVTileEraser)target;
            
            //target meshes
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetMeshes"), true);
            
            //UV Channel as dropdown
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UV Channel", EditorStyles.boldLabel);
            eraser.uvChannel = EditorGUILayout.Popup(eraser.uvChannel, uvChannelNames);
            
            //grid mode toggle
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
            eraser.useAdvancedMode = EditorGUILayout.Toggle("Advanced Mode", eraser.useAdvancedMode);
            
            if (eraser.useAdvancedMode)
            {
                EditorGUILayout.HelpBox("Advanced mode allows tiles outside the standard 0-3 range", MessageType.Info);
                EditorGUI.indentLevel++;
                eraser.minU = EditorGUILayout.IntSlider("Min U", eraser.minU, -64, 64);
                eraser.maxU = EditorGUILayout.IntSlider("Max U", eraser.maxU, -64, 64);
                eraser.minV = EditorGUILayout.IntSlider("Min V", eraser.minV, -64, 64);
                eraser.maxV = EditorGUILayout.IntSlider("Max V", eraser.maxV, -64, 64);
                
                //validate ranges
                if (eraser.maxU < eraser.minU) eraser.maxU = eraser.minU;
                if (eraser.maxV < eraser.minV) eraser.maxV = eraser.minV;
                EditorGUI.indentLevel--;
            }
            
            //tile grid
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tiles to Erase", EditorStyles.boldLabel);
            if (eraser.useAdvancedMode)
            {
                DrawAdvancedTileGrid(eraser);
            }
            else
            {
                DrawStandardTileGrid(eraser);
            }
            
            //erase mode
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eraseIfAnyVertex"), 
                new GUIContent("Erase Mode", "Any Vertex: Erase if ANY vertex is in tile\nAll Vertices: Erase only if ALL vertices are in tile"));
            
            //material filter
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Material Filter (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Leave empty to erase from all materials. Add materials to only affect specific ones.", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialFilter"), true);
            
            if (eraser.materialFilter != null && eraser.materialFilter.Count > 0)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("includeMaterials"), 
                    new GUIContent("Filter Mode", "Include: Only erase faces with these materials\nExclude: Erase all except these materials"));
                
                //show which materials will be affected
                EditorGUILayout.HelpBox(
                    eraser.includeMaterials 
                        ? $"Will only erase faces using the {eraser.materialFilter.Count} specified material(s)" 
                        : $"Will erase all faces EXCEPT those using the {eraser.materialFilter.Count} specified material(s)", 
                    MessageType.Info);
            }
            
            //stats
            EditorGUILayout.Space();
            if (eraser.targetMeshes != null && eraser.targetMeshes.Count > 0)
            {
                var validCount = eraser.targetMeshes.Count(m => m != null);
                EditorGUILayout.HelpBox($"{validCount} mesh(es) will be processed", MessageType.Info);
            }
            
            EditorGUILayout.HelpBox("Note: Meshes support UV channels 0-7. Make sure your meshes have UVs in the selected channel.", MessageType.None);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawStandardTileGrid(UVTileEraser eraser)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            //draw grid from top to bottom (v=3 to v=0)
            for (int v = 3; v >= 0; v--)
            {
                EditorGUILayout.BeginHorizontal();
                
                for (int u = 0; u < 4; u++)
                {
                    int index = v * 4 + u;
                    
                    //create toggle button style
                    var style = new GUIStyle(GUI.skin.button);
                    if (eraser.tilesToErase.tiles[index])
                    {
                        style.normal.background = Texture2D.whiteTexture;
                        style.normal.textColor = Color.red;
                        style.fontStyle = FontStyle.Bold;
                    }
                    
                    //draw toggle button
                    if (GUILayout.Button($"{u},{v}", style, GUILayout.Width(40), GUILayout.Height(40)))
                    {
                        Undo.RecordObject(eraser, "Toggle UV Tile");
                        eraser.tilesToErase.tiles[index] = !eraser.tilesToErase.tiles[index];
                        EditorUtility.SetDirty(eraser);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            
            //quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All", GUILayout.Height(20)))
            {
                Undo.RecordObject(eraser, "Clear All Tiles");
                for (int i = 0; i < 16; i++)
                    eraser.tilesToErase.tiles[i] = false;
                EditorUtility.SetDirty(eraser);
            }
            if (GUILayout.Button("Select All", GUILayout.Height(20)))
            {
                Undo.RecordObject(eraser, "Select All Tiles");
                for (int i = 0; i < 16; i++)
                    eraser.tilesToErase.tiles[i] = true;
                EditorUtility.SetDirty(eraser);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("Click tiles to toggle. Red = will be erased. Tile (0,0) is bottom-left.", MessageType.None);
        }
        
        private void DrawAdvancedTileGrid(UVTileEraser eraser)
        {
            var tileSize = 30;
            var gridWidth = eraser.maxU - eraser.minU + 1;
            var gridHeight = eraser.maxV - eraser.minV + 1;
            
            //warning for large grids
            if (gridWidth * gridHeight > 256)
            {
                EditorGUILayout.HelpBox("Large grid! Performance may be impacted.", MessageType.Warning);
            }
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(400));
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            //draw grid from top to bottom
            for (int v = eraser.maxV; v >= eraser.minV; v--)
            {
                EditorGUILayout.BeginHorizontal();
                
                for (int u = eraser.minU; u <= eraser.maxU; u++)
                {
                    var tileKey = new Vector2Int(u, v);
                    bool isSelected = eraser.tilesToErase.customTiles.ContainsKey(tileKey) && eraser.tilesToErase.customTiles[tileKey];
                    
                    //create toggle button style
                    var style = new GUIStyle(GUI.skin.button);
                    style.fontSize = 9;
                    if (isSelected)
                    {
                        style.normal.background = Texture2D.whiteTexture;
                        style.normal.textColor = Color.red;
                        style.fontStyle = FontStyle.Bold;
                    }
                    
                    //draw toggle button
                    if (GUILayout.Button($"{u},{v}", style, GUILayout.Width(tileSize), GUILayout.Height(tileSize)))
                    {
                        Undo.RecordObject(eraser, "Toggle UV Tile");
                        if (eraser.tilesToErase.customTiles.ContainsKey(tileKey))
                        {
                            eraser.tilesToErase.customTiles[tileKey] = !eraser.tilesToErase.customTiles[tileKey];
                        }
                        else
                        {
                            eraser.tilesToErase.customTiles[tileKey] = true;
                        }
                        EditorUtility.SetDirty(eraser);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            
            //quick actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All", GUILayout.Height(20)))
            {
                Undo.RecordObject(eraser, "Clear All Tiles");
                eraser.tilesToErase.customTiles.Clear();
                EditorUtility.SetDirty(eraser);
            }
            if (GUILayout.Button("Select Visible", GUILayout.Height(20)))
            {
                Undo.RecordObject(eraser, "Select Visible Tiles");
                for (int v = eraser.minV; v <= eraser.maxV; v++)
                {
                    for (int u = eraser.minU; u <= eraser.maxU; u++)
                    {
                        eraser.tilesToErase.customTiles[new Vector2Int(u, v)] = true;
                    }
                }
                EditorUtility.SetDirty(eraser);
            }
            EditorGUILayout.EndHorizontal();
            
            var selectedCount = eraser.tilesToErase.customTiles.Count(kvp => kvp.Value);
            EditorGUILayout.HelpBox($"Advanced mode. {selectedCount} tiles selected. Click tiles to toggle.", MessageType.None);
        }
    }
}