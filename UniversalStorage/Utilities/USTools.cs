using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace UniversalStorage2
{
  public static class USTools
  {
    private static Material _material;

    private static int glDepth = 0;

    private static Material material
    {
      get
      {
        if (_material == null) _material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        return _material;
      }
    }

    public static List<int> parseIntegers(string stringOfInts, char sep = ';')
    {
      var newIntList = new List<int>();
      var valueArray = stringOfInts.Split(sep);
      for (var i = 0; i < valueArray.Length; i++)
      {
        var newValue = 0;

        if (int.TryParse(valueArray[i], out newValue))
          newIntList.Add(newValue);
        else
          USdebugMessages.USStaticLog("Error parsing: Invalid integer - {0}", valueArray[i]);
      }

      return newIntList;
    }

    public static List<double> parseDoubles(string stringOfDoubles, char sep = ';')
    {
      var list = new List<double>();
      var array = stringOfDoubles.Trim().Split(sep);
      for (var i = 0; i < array.Length; i++)
      {
        double item = 0f;
        if (double.TryParse(array[i].Trim(), out item))
          list.Add(item);
        else
          USdebugMessages.USStaticLog("Error parsing: Invalid double - {0}", array[i]);
      }

      return list;
    }

    public static List<float> parseSingles(string stringOfSingles, char sep = ';')
    {
      var list = new List<float>();
      var array = stringOfSingles.Trim().Split(sep);
      for (var i = 0; i < array.Length; i++)
      {
        var item = 0f;
        if (float.TryParse(array[i].Trim(), out item))
          list.Add(item);
        else
          USdebugMessages.USStaticLog("Error parsing: Invalid float - {0}", array[i]);
      }

      return list;
    }

    public static List<Vector3> parseVectors(string stringOfVectors, char sep = ';')
    {
      var list = new List<Vector3>();
      var array = stringOfVectors.Trim().Split(sep);
      for (var i = 0; i < array.Length; i++)
      {
        var vec = Vector3.zero;
        var floats = array[i].Trim().Split(',');
        for (var j = 0; j < floats.Length; j++)
        {
          var item = 0f;
          if (float.TryParse(floats[j].Trim(), out item))
          {
            if (j < 3)
              vec[j] = item;
          }
          else
          {
            USdebugMessages.USStaticLog("Error parsing: Invalid float for Vector3 - {0}", array[i]);
          }
        }

        list.Add(vec);
      }

      return list;
    }

    public static List<List<string>> parseDoubleStrings(string names, char sep = ';', char secondSep = '|')
    {
      var strings = new List<List<string>>();

      var array = names.Trim().Split(secondSep);

      for (var i = 0; i < array.Length; i++)
      {
        var secondArray = array[i].Trim().Split(sep);

        var values = new List<string>();

        for (var j = 0; j < secondArray.Length; j++) values.Add(secondArray[j].Trim());

        strings.Add(values);
      }

      return strings;
    }

    public static List<string> parseNames(string names, char sep = ';')
    {
      return parseNames(names, false, true, string.Empty, sep);
    }

    public static List<string> parseNames(string names, bool replaceBackslashErrors, char sep = ';')
    {
      return parseNames(names, replaceBackslashErrors, true, string.Empty, sep);
    }

    public static List<string> parseNames(string names, bool replaceBackslashErrors, bool trimWhiteSpace, string prefix,
      char sep = ';')
    {
      var source = names.Split(sep).ToList<string>();
      for (var i = source.Count - 1; i >= 0; i--)
        if (source[i] == string.Empty)
          source.RemoveAt(i);
      if (trimWhiteSpace)
        for (var i = 0; i < source.Count; i++)
          source[i] = source[i].Trim(' ');
      if (prefix != string.Empty)
        for (var i = 0; i < source.Count; i++)
          source[i] = prefix + source[i];
      if (replaceBackslashErrors)
        for (var i = 0; i < source.Count; i++)
          source[i] = source[i].Replace('\\', '/');
      return source.ToList<string>();
    }

    public static List<List<Transform>> parseObjectNames(string batch, Part part)
    {
      var transformBatches = new List<List<Transform>>();

      var batches = batch.Split(';');

      for (var i = 0; i < batches.Length; i++)
      {
        var transforms = new List<Transform>();

        var objectNames = batches[i].Split(',');

        for (var j = 0; j < objectNames.Length; j++)
        {
          var newTransform = part.FindModelTransform(objectNames[j].Trim(' '));

          if (newTransform != null)
            transforms.Add(newTransform);
        }

        transformBatches.Add(transforms);
      }

      return transformBatches;
    }

    public static List<Transform> parseTransformNames(string batch, Part part)
    {
      var transforms = new List<Transform>();

      var objectNames = batch.Split(';');

      for (var i = 0; i < objectNames.Length; i++)
      {
        var newTransform = part.FindModelTransform(objectNames[i].Trim(' '));

        if (newTransform != null) transforms.Add(newTransform);
      }

      return transforms;
    }

    public static List<List<string>> parseDragCubes(string batch, Part part)
    {
      var cubeBatches = new List<List<string>>();

      var batches = batch.Split(';');

      for (var i = 0; i < batches.Length; i++)
      {
        var cubes = new List<string>();

        var cubeNames = batches[i].Split(',');

        for (var j = 0; j < cubeNames.Length; j++)
        {
          var cube = cubeNames[j];

          for (var k = part.DragCubes.Cubes.Count - 1; k >= 0; k--)
          {
            var d = part.DragCubes.Cubes[k];

            if (d.Name != cube)
              continue;

            cubes.Add(cube);
            break;
          }
        }

        cubeBatches.Add(cubes);
      }

      return cubeBatches;
    }

    public static List<List<ModuleStructuralNode>> parseStructuralNodes(string batch, Part part)
    {
      var nodeBatches = new List<List<ModuleStructuralNode>>();

      var batches = batch.Split(';');

      var modNodes = part.FindModulesImplementing<ModuleStructuralNode>();

      for (var i = 0; i < batches.Length; i++)
      {
        var nodes = new List<ModuleStructuralNode>();

        var nodeNames = batches[i].Split(',');

        for (var j = 0; j < nodeNames.Length; j++)
        {
          for (var k = modNodes.Count - 1; k >= 0; k--)
          {
            var node = modNodes[k];

            if (node == null)
              continue;

            if (node.rootObject != nodeNames[j])
              continue;

            node.visibilityState = false;

            nodes.Add(node);
            break;
          }

          nodeBatches.Add(nodes);
        }
      }

      return nodeBatches;
    }

    public static List<AttachNode> parseAttachNodes(string nodes, Part part)
    {
      var attachNodes = new List<AttachNode>();

      var nodeNames = nodes.Split(';');

      for (var i = 0; i < nodeNames.Length; i++)
      for (var j = part.attachNodes.Count - 1; j >= 0; j--)
      {
        if (part.attachNodes[j].id != nodeNames[i])
          continue;

        //USdebugMessages.USStaticLog("Parse Attach Node: {0}", nodeNames[i]);

        attachNodes.Add(part.attachNodes[j]);
        break;
      }

      return attachNodes;
    }

    public static Bounds GetActivePartBounds(GameObject part)
    {
      var boundy = default(Bounds);

      var pos = part.transform.position;
      var quat = part.transform.rotation;

      part.transform.position = Vector3.zero;
      part.transform.rotation = Quaternion.identity;

      var renderers = part.GetComponentsInChildren<Renderer>();

      var bounded = false;

      for (var i = 0; i < renderers.Length; i++)
      {
        //Bounds b = renderers[i].bounds;
        //USdebugMessages.USStaticLog("Object: {0} - Active: {1}\nCenter: {2:F3} Size: {3:F3}\nMin: {4:F3} Max: {5:F3}"
        //    , renderers[i].name, renderers[i].gameObject.activeInHierarchy,
        //    b.center, b.size, b.min, b.max);

        if (!renderers[i].gameObject.activeInHierarchy)
          continue;

        if (renderers[i] is MeshRenderer)
        {
          var mesh = renderers[i].gameObject.GetComponent<MeshFilter>();

          var b = mesh.sharedMesh.bounds;

          //USdebugMessages.USStaticLog("Object: {0} - Active: {1}\nCenter: {2:F3} Size: {3:F3}\nMin: {4:F3} Max: {5:F3}"
          //, renderers[i].name, renderers[i].gameObject.activeInHierarchy,
          //b.center, b.size, b.min, b.max);

          if (!bounded)
          {
            boundy = new Bounds(b.center, b.size);
            bounded = true;
          }
          else
          {
            boundy.Encapsulate(b);
            //boundy.Encapsulate(renderers[i].bounds.max);
          }
        }
      }

      part.transform.position = pos;
      part.transform.rotation = quat;

      return boundy;
    }

    public static float GetBoundsScale(Bounds bounder)
    {
      float f = 1;

      f = Mathf.Max(Mathf.Abs(bounder.size.y), Mathf.Abs(bounder.size.z));

      f = Mathf.Max(Mathf.Abs(bounder.size.x), f);

      f = 1 / f;

      return f;
    }

    private static void GLStart()
    {
      if (glDepth == 0)
      {
        GL.PushMatrix();
        material.SetPass(0);
        GL.LoadPixelMatrix();
        GL.Begin(GL.LINES);
      }

      glDepth++;
    }

    private static void GLEnd()
    {
      glDepth--;

      if (glDepth == 0)
      {
        GL.End();
        GL.PopMatrix();
      }
    }

    private static Camera GetActiveCam()
    {
      Camera cam;
      if (HighLogic.LoadedSceneIsEditor)
        cam = EditorLogic.fetch.editorCamera;
      else if (HighLogic.LoadedSceneIsFlight)
        cam = MapView.MapIsEnabled ? PlanetariumCamera.Camera : FlightCamera.fetch.mainCamera;
      else
        cam = Camera.main;
      return cam;
    }

    private static void DrawLine(Vector3 origin, Vector3 destination, Color color)
    {
      var cam = GetActiveCam();

      var screenPoint1 = cam.WorldToScreenPoint(origin);
      var screenPoint2 = cam.WorldToScreenPoint(destination);

      GL.Color(color);
      GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
      GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
    }

    public static void DrawSphere(Vector3 position, Color color, float radius = 1.0f)
    {
      var segments = 36;
      var step = Mathf.Deg2Rad * 360f / segments;

      var x = new Vector3(position.x, position.y, position.z + radius);
      var y = new Vector3(position.x + radius, position.y, position.z);
      var z = new Vector3(position.x + radius, position.y, position.z);

      GLStart();
      GL.Color(color);

      for (var i = 1; i <= segments; i++)
      {
        var angle = step * i;
        var nextX = new Vector3(position.x, position.y + radius * Mathf.Sin(angle),
          position.z + radius * Mathf.Cos(angle));
        var nextY = new Vector3(position.x + radius * Mathf.Cos(angle), position.y,
          position.z + radius * Mathf.Sin(angle));
        var nextZ = new Vector3(position.x + radius * Mathf.Cos(angle), position.y + radius * Mathf.Sin(angle),
          position.z);

        DrawLine(x, nextX, color);
        DrawLine(y, nextY, color);
        DrawLine(z, nextZ, color);

        x = nextX;
        y = nextY;
        z = nextZ;
      }

      GLEnd();
    }

    // Code from https://github.com/Swamp-Ig/KSPAPIExtensions/blob/master/Source/Utils/KSPUtils.cs#L62
    private static FieldInfo windowListField;

    /// <summary>
    /// Find the UIPartActionWindow for a part. Usually this is useful just to mark it as dirty.
    /// </summary>
    public static UIPartActionWindow FindActionWindow(this Part part)
    {
      if (part == null)
        return null;

      // We need to do quite a bit of piss-farting about with reflection to 
      // dig the thing out. We could just use Object.Find, but that requires hitting a heap more objects.
      var controller = UIPartActionController.Instance;
      if (controller == null)
        return null;

      return controller.GetItem(part, false);

      //	if (windowListField == null)
      //	{
      //		Type cntrType = typeof(UIPartActionController);
      //		foreach (FieldInfo info in cntrType.GetFields(BindingFlags.Instance))
      //		{
      //			if (info.FieldType == typeof(List<UIPartActionWindow>))
      //			{
      //				windowListField = info;
      //				goto foundField;
      //			}
      //		}

      //              Debug.LogWarning("*PartUtils* Unable to find UIPartActionWindow list");
      //		return null;
      //	}
      //foundField:

      //	List<UIPartActionWindow> uiPartActionWindows = (List<UIPartActionWindow>)windowListField.GetValue(controller);
      //	if (uiPartActionWindows == null)
      //		return null;

      //	return uiPartActionWindows.FirstOrDefault(window => window != null && window.part == part);
    }
  }
}