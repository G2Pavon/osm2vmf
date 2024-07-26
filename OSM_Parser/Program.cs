﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OSM_Parser
{
	class Settings
	{
		public static double scale = 1;
		public static double yAndgle = -29;//NewYork
		public static string wallsMaterial = "building_template/building_template007c";
		public static string roofMaterial = "building_template/roof_template001a";
		public static string ceilingMaterial = "concrete/concretefloor001a";
		public static int lightmapscale = 32;
		public static bool cull = true;
		public static Rect2D cullBounding = new Rect2D(-310,-400,610,800);//Rect2D(-150, -170, 300, 410);//
	}

	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Usage();
				return;
			}

			string filename = args[0];
			bool exportVMF = true;
			bool exportOBJ = false;
			bool exportMAP = false;

			if (!filename.EndsWith(".osm", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("You must provide the input .osm file as first argument.");
				return;
			}

			for (int i = 1; i < args.Length; i++)
			{
				switch (args[i].ToLower())
				{
					case "-obj":
						exportOBJ = true;
						break;
					case "-map":
						exportMAP = true;
						break;
					case "-novmf":
						exportVMF = false;
						break;
					default:
						Console.WriteLine($"Invalid argument: {args[i]}");
						Usage();
						return;
				}
			}

			if (!exportVMF && !exportOBJ && !exportMAP)
			{
				Console.WriteLine("-novmf argument is only valid if -obj or -map is specified.");
				Usage();
				return;
			}

			OsmChunk map = new OsmChunk();
			if (map.Load(filename))
			{
				return;
			}

			if (Settings.cull)
			{
				map.Cull(Settings.cullBounding);
			}

			if (exportOBJ)
			{
				ExportOBJ(filename.Replace(".osm", ""), map);
			}

			if (exportMAP)
			{
				ExportMAP(filename.Replace(".osm", ".map"), map);
			}

			if (exportVMF)
			{
				ExportVMF(filename.Replace(".osm", ".vmf"), map);
			}
		}

		static void Usage()
		{
			Console.WriteLine("OpenStreetMap to Valve Map Format converter. Also support export OBJ and GoldSrc MAP: https://github.com/lewa-j/osm2vmf\n");
			Console.WriteLine("You must provide the input .osm file as first argument. Export OSM data from: https://www.openstreetmap.org");
			Console.WriteLine("Usage: osm2vmf <input.osm> [-obj] [-map] [-novmf]");
			Console.WriteLine("Optional Arguments:");
			Console.WriteLine("\t-obj: export as .OBJ");
			Console.WriteLine("\t-map: export as GoldSrc .MAP");
			Console.WriteLine("\t-novmf: do not export as .VMF");
		}

		public static void ExportOBJ(string filename, OsmChunk map)
		{
			filename = filename.Replace(".\\","");

			StreamWriter msw = new StreamWriter(filename + ".mtl");
			msw.Write("newmtl roof_mat\n\n");
			msw.Write("newmtl wall_mat\n\n");

			msw.Close();

			StreamWriter sw = new StreamWriter(filename+".obj");
			sw.Write("mtllib "+filename+".mtl\n");
			sw.Write("o "+filename+"\n\n");

			/*
			for (int i = 0; i < map.nodes.Count; i++)
			{
				OsmNode n = map.nodes.ElementAt(i).Value;
				vec3 v = map.GetNodeCoords(n);
				v *= 0.05f;
				sw.Write("v " + v.ToString() + "\n");
			}
			for (int i = 0; i < map.buildings.Count; i++)
			{
				OsmBuilding bd = map.buildings[i];

				sw.Write("f");
				for (int j = 0; j < bd.nodeIds.Length - 1; j++)
				{
					long id = map.nodes.Keys.ToList().IndexOf(bd.nodeIds[j]);
					sw.Write(" " + (id + 1));
				}
				sw.Write("\n");
			}
			*/
			int v=0;
			for (int i = 0; i < map.buildings.Count; i++)
			{
				OsmBuilding bd = map.buildings[i];
				//if (bd.outline)
				//	continue;
				Polygon[] polys = map.GetBuildingPolys(bd);

				//flat roof
				sw.Write("usemtl roof_mat\n");
				sw.Write("s off\n");
				for (int j = 0; j < polys.Length; j++)
				{
					for (int k = 0; k < polys[j].verts.Length; k++)
					{
						vec3 v1 = polys[j].verts[k];
						v1.y = bd.height;

						v1 *= Settings.scale;
						sw.Write("v " + v1.ToString() + "\n");
					}

					sw.Write("f");
					for (int k = 0; k < polys[j].verts.Length; k++)
					{
						//long id = map.nodes.Keys.ToList().IndexOf(bd.nodeIds[j]);
						//sw.Write(" " + (id + 1));
						v++;
						sw.Write(" " + v);
					}
					sw.Write("\n\n");
				}

				if (bd.min_height > 0.001)
				{
					for (int j = 0; j < polys.Length; j++)
					{
						for (int k = polys[j].verts.Length-1; k >= 0; k--)
						{
							vec3 v1 = polys[j].verts[k];
							v1.y = bd.min_height;
							v1 *= Settings.scale;
							sw.Write("v " + v1.ToString() + "\n");
						}

						sw.Write("f");
						for (int k = 0; k < polys[j].verts.Length; k++)
						{
							//long id = map.nodes.Keys.ToList().IndexOf(bd.nodeIds[j]);
							//sw.Write(" " + (id + 1));
							v++;
							sw.Write(" " + v);
						}
						sw.Write("\n\n");
					}
				}

				//walls
				sw.Write("usemtl wall_mat\n");
				sw.Write("s off\n");
				for (int j = 0; j < bd.nodeIds.Length - 1; j++)
				{
					vec3 v1 = map.GetNodeCoords(map.nodes[bd.nodeIds[j]]);
					vec3 v2 = map.GetNodeCoords(map.nodes[bd.nodeIds[j+1]]);
					vec3 v3 = v2;
					vec3 v4 = v1;

					v1.y = bd.height;
					v2.y = bd.height;
					v3.y = bd.min_height;
					v4.y = bd.min_height;
					v1 *= Settings.scale;
					v2 *= Settings.scale;
					v3 *= Settings.scale;
					v4 *= Settings.scale;
					sw.Write("v " + v1.ToString() + "\n");
					sw.Write("v " + v2.ToString() + "\n");
					sw.Write("v " + v3.ToString() + "\n");
					sw.Write("v " + v4.ToString() + "\n");

					sw.Write("f {0} {1} {2} {3}\n",v+1,v+2,v+3,v+4);
					v += 4;
				}
				sw.Write("\n");
			}

			Console.WriteLine("obj: verts count {0}",v);

			sw.Close();
		}

		public static void ExportVMF(string filename, OsmChunk map)
		{
			long brushId = 1;
			StreamWriter sw = new StreamWriter(filename);
			/*versioninfo
			{
				"formatversion" "100"
				"prefab" "1"
			}*/
			sw.Write("versioninfo\n{\n\"formatversion\" \"100\"\n\"prefab\" \"1\"\n}\n");
			/*visgroups
			{
			}
			viewsettings
			{
			}*/
			
			double s = 30.0 * Settings.scale;
			sw.Write("world\n{\n");
			sw.Write("\"id\" \"1\"\n\"classname\" \"worldspawn\"\n\"skyname\" \"sky_day01_01\"\n");
			//
			for (int i = 0; i < map.buildings.Count; i++)
			{
				OsmBuilding bd = map.buildings[i];
				//if (bd.outline)
				//	continue;
				double h1 = bd.min_height * s;
				double h2 = bd.height * s;
				if (h2 > 30000) {
					Console.WriteLine("building {0} is too high {1}",i,h2);
				}

				Polygon[] polys = map.GetBuildingPolys(bd);
				for (int p = 0; p < polys.Length; p++)
				{
					sw.Write("solid\n{\n");
					sw.Write("\t\"id\" \"{0}\"\n",brushId);
					brushId++;
					for (int j = 0; j < polys[p].verts.Length; j++)
					{
						vec3 v1 = polys[p].At(j);
						vec3 v2 = polys[p].At(j + 1);
						vec3 v3 = new vec3(v2.x, h2, v2.z);

						v1.y = h1;
						v2.y = h1;

						v1 *= s;
						v2 *= s;
						v3 *= s;

						v1.z *= -1;
						v2.z *= -1;
						v3.z *= -1;

						//v1 += (v1 - v3)*4;
						//v2 += (v2 - v3)*4;

						v1 = new vec3(v1.x, v1.z, v1.y).Snap();
						v2 = new vec3(v2.x, v2.z, v2.y).Snap();
						v3 = new vec3(v3.x, v3.z, v3.y).Snap();

						vec3 uaxis = new vec3(v2.x - v1.x, v2.y - v1.y, 0);
						uaxis.Normalize();

						sw.Write("\tside\n\t{\n");
						sw.Write("\t\t\"plane\" \"(" + v1.ToString() + ") (" + v2.ToString() + ") (" + v3.ToString() + ")\"\n");
						sw.Write("\t\t\"material\" \"" + Settings.wallsMaterial + "\"\n");
						sw.Write("\t\t\"uaxis\" \"[" + uaxis.ToString() + " 0] 0.25\"\n");
						sw.Write("\t\t\"vaxis\" \"[0 0 -1 0] 0.25\"\n");
						//"rotation" "0"
						sw.Write("\t\t\"lightmapscale\" \""+Settings.lightmapscale+"\"\n");
						sw.Write("\t}\n");
					}
					//top
					sw.Write("\tside\n\t{\n");
					sw.Write("\t\t\"plane\" \"(" + new vec3(0, 50, h2).ToString() + ") (" + new vec3(50, 50, h2).ToString() + ") (" + new vec3(50, 0, h2).ToString() + ")\"\n");
					sw.Write("\t\t\"material\" \""+Settings.roofMaterial+"\"\n");
					sw.Write("\t\t\"uaxis\" \"[1 0 0 0] 0.25\"\n");
					sw.Write("\t\t\"vaxis\" \"[0 -1 0 0] 0.25\"\n");
					sw.Write("\t\t\"lightmapscale\" \"" + Settings.lightmapscale + "\"\n");
					sw.Write("\t}\n");
					//bottom
					sw.Write("\tside\n\t{\n");
					sw.Write("\t\t\"plane\" \"(" + new vec3(0, 0, h1).ToString() + ") (" + new vec3(50, 0, h1).ToString() + ") (" + new vec3(50, 50, h1).ToString() + ")\"\n");
					sw.Write("\t\t\"material\" \""+Settings.ceilingMaterial+"\"\n");
					sw.Write("\t\t\"uaxis\" \"[1 0 0 0] 0.25\"\n");
					sw.Write("\t\t\"vaxis\" \"[0 -1 0 0] 0.25\"\n");
					sw.Write("\t\t\"lightmapscale\" \"" + Settings.lightmapscale + "\"\n");
					sw.Write("\t}\n");

					sw.Write("}\n");//end solid
				}
			}

			//sky and ground
			//(-150, -170, 300, 410)
			/*VMFWriteSolid(sw, brushId++, new vec3(-5120, -7680, 8128), new vec3(5120, 5312, 8192), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-5120, -7680, 0), new vec3(-5056, 5312, 8128), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(5056, -7680, 0), new vec3(5120, 5312, 8128), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-5056, 5248, 0), new vec3(5056, 5312, 8128), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-5056, -7680, 0), new vec3(5056, -7616, 8128), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-5120, -7680, -64), new vec3(5120, 5312, 0), "concrete/concretefloor005a");*/
			VMFWriteSolid(sw, brushId++, new vec3(-12288, -15360, 14000), new vec3(12288, 12288, 14064), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-12288, -15360, -64), new vec3(12288, 12288, 0), "concrete/concretefloor005a");
			VMFWriteSolid(sw, brushId++, new vec3(-12288, -15360, 0), new vec3(-12224, 12288, 14064), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(12224, -15360, 0), new vec3(12288, 12288, 14064), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-12288, 12224, 0), new vec3(12288, 12288, 14064), "tools/toolsskybox");
			VMFWriteSolid(sw, brushId++, new vec3(-12288, -15360, 0), new vec3(12288, -15296, 14064), "tools/toolsskybox");

			sw.Write("}\n");//end world

			//light
			sw.Write("entity\n{\n");
			//sw.Write("\t\"id\" \"{0}\"\n",id);
			sw.Write("\t\"classname\" \"light_environment\"\n");
			sw.Write("\t\"angles\" \"0 345 0\"\n");
			sw.Write("\t\"pitch\" \"-60\"\n");
			sw.Write("\t\"SunSpreadAngle\" \"1\"\n");
			sw.Write("\t\"_ambient\" \"255 255 255 20\"\n");
			sw.Write("\t\"_ambientHDR\" \"-1 -1 -1 1\"\n");
			sw.Write("\t\"_AmbientScaleHDR\" \"1\"\n");
			sw.Write("\t\"_light\" \"255 255 255 200\"\n");
			sw.Write("\t\"_lightHDR\" \"-1 -1 -1 1\"\n");
			sw.Write("\t\"_lightscaleHDR\" \"1\"\n");
			sw.Write("\t\"origin\" \"-320 -4607 128\"\n");
			sw.Write("}\n");

			//player
			sw.Write("entity\n{\n");
			//sw.Write("\t\"id\" \"{0}\"\n",id);
			sw.Write("\t\"classname\" \"info_player_start\"\n");
			sw.Write("\t\"angles\" \"0 0 0\"\n");
			sw.Write("\t\"origin\" \"-384 -4601 16\"\n");
			sw.Write("}\n");

			sw.Close();

			Console.WriteLine("vmf: brushes count {0}", brushId);
		}

		public static void VMFWriteSolid(StreamWriter sw, long brushId, vec3 bbmin, vec3 bbmax, string material)
		{
			sw.Write("solid\n{\n");
			sw.Write("\t\"id\" \"{0}\"\n", brushId);

			WVFWriteSide(sw, new vec3(bbmin.x, bbmax.y, bbmax.z), bbmax,                               new vec3(bbmax.x,bbmin.y,bbmax.z), new vec3(0, - 1, 0), material);
			WVFWriteSide(sw, new vec3(bbmin.x, bbmax.y, bbmax.z), new vec3(bbmin.x, bbmin.y, bbmax.z), bbmin, new vec3(0, 0, -1), material);
			WVFWriteSide(sw, new vec3(bbmax.x, bbmax.y, bbmin.z), new vec3(bbmax.x, bbmin.y, bbmin.z), new vec3(bbmax.x,bbmin.y,bbmax.z), new vec3(0, 0, -1), material);
			WVFWriteSide(sw, bbmax,                               new vec3(bbmin.x, bbmax.y, bbmax.z), new vec3(bbmin.x, bbmax.y, bbmin.z), new vec3(0, 0, -1), material);
			WVFWriteSide(sw, new vec3(bbmax.x, bbmin.y, bbmin.z), bbmin,                               new vec3(bbmin.x, bbmin.y, bbmax.z), new vec3(0, 0, -1), material);
			WVFWriteSide(sw, new vec3(bbmax.x, bbmin.y, bbmin.z), new vec3(bbmax.x, bbmax.y, bbmin.z), new vec3(bbmin.x, bbmax.y, bbmin.z), new vec3(-1, 0, 0), material);

			sw.Write("}\n");//end solid
		}
		public static void WVFWriteSide(StreamWriter sw, vec3 v1, vec3 v2, vec3 v3, vec3 vaxis, string material)
		{

			vec3 uaxis = new vec3(v2.x - v1.x, v2.y - v1.y, 0);
			uaxis.Normalize();

			sw.Write("\tside\n\t{\n");
			sw.Write("\t\t\"plane\" \"(" + v1.ToString() + ") (" + v2.ToString() + ") (" + v3.ToString() + ")\"\n");
			sw.Write("\t\t\"material\" \"" + material + "\"\n");
			sw.Write("\t\t\"uaxis\" \"[" + uaxis.ToString() + " 0] 0.25\"\n");
			sw.Write("\t\t\"vaxis\" \"[" + vaxis.ToString() + " 0] 0.25\"\n");
			//"rotation" "0"
			sw.Write("\t\t\"lightmapscale\" \"" + Settings.lightmapscale + "\"\n");
			sw.Write("\t}\n");
		}

		public static void ExportMAP(string filename, OsmChunk map)
		{
			long brushId = 1;
			StreamWriter sw = new StreamWriter(filename);	
			double s = 30.0 * Settings.scale;

			sw.Write("// Game: Half-Life\n");
			sw.Write("// Format: Valve\n");
			sw.Write("\n{\n");
			sw.Write("\"wad\" \"\"\n");
			sw.Write("\"classname\" \"worldspawn\"\n");
			sw.Write("\"mapversion\" \"220\"\n");
			//
			for (int i = 0; i < map.buildings.Count; i++)
			{
				OsmBuilding bd = map.buildings[i];
				//if (bd.outline)
				//	continue;
				double h1 = bd.min_height * s;
				double h2 = bd.height * s;
				if (h2 > 30000) {
					Console.WriteLine("building {0} is too high {1}",i,h2);
				}

				Polygon[] polys = map.GetBuildingPolys(bd);
				for (int p = 0; p < polys.Length; p++)
				{
					sw.Write("\n{\n");
					brushId++;
					for (int j = 0; j < polys[p].verts.Length; j++)
					{
						vec3 v1 = polys[p].At(j);
						vec3 v2 = polys[p].At(j + 1);
						vec3 v3 = new vec3(v2.x, h2, v2.z);

						v1.y = h1;
						v2.y = h1;

						v1 *= s;
						v2 *= s;
						v3 *= s;

						v1.z *= -1;
						v2.z *= -1;
						v3.z *= -1;

						v1 = new vec3(v1.x, v1.z, v1.y).Snap();
						v2 = new vec3(v2.x, v2.z, v2.y).Snap();
						v3 = new vec3(v3.x, v3.z, v3.y).Snap();

						vec3 uaxis = new vec3(v2.x - v1.x, v2.y - v1.y, 0);
						uaxis.Normalize();

						sw.Write("( " + v1.ToString() + " ) ( " + v2.ToString() + " ) ( " + v3.ToString() + " ) ");
						sw.Write("wall");
						sw.Write(" [ " + uaxis.ToString() + " 0 ]");
						sw.Write(" [ 0 0 -1 0 ]");
						sw.Write(" 0 1.0 1.0");
						sw.Write("\n");
					}
					//top
					sw.Write("( " + new vec3(0, 50, h2).ToString() + " ) ( " + new vec3(50, 50, h2).ToString() + " ) ( " + new vec3(50, 0, h2).ToString() + " ) ");
					sw.Write("roof");
					sw.Write(" [ 1 0 0 0 ]");
					sw.Write(" [ 0 -1 0 0 ]");
					sw.Write(" 0 1.0 1.0");
					sw.Write("\n");
					//bottom
					sw.Write("( " + new vec3(0, 0, h1).ToString() + " ) ( " + new vec3(50, 0, h1).ToString() + " ) ( " + new vec3(50, 50, h1).ToString() + " ) ");
					sw.Write("bottom");
					sw.Write(" [ 1 0 0 0 ]");
					sw.Write(" [ 0 -1 0 0 ]");
					sw.Write(" 0 1.0 1.0");
					sw.Write("\n}\n");//end solid
				}
			}

			//sky and ground
			MAPWriteSolid(sw, new vec3(-12288, -15360, 14000), new vec3(12288, 12288, 14064), "sky");
			MAPWriteSolid(sw, new vec3(-12288, -15360, 0), new vec3(-12224, 12288, 14064), "sky");
			MAPWriteSolid(sw, new vec3(12224, -15360, 0), new vec3(12288, 12288, 14064), "sky");
			MAPWriteSolid(sw, new vec3(-12288, 12224, 0), new vec3(12288, 12288, 14064), "sky");
			MAPWriteSolid(sw, new vec3(-12288, -15360, 0), new vec3(12288, -15296, 14064), "sky");
			MAPWriteSolid(sw, new vec3(-12288, -15360, -64), new vec3(12288, 12288, 0), "floor");

			sw.Write("}\n");//end world
			sw.Close();

			Console.WriteLine("MAP: brushes count {0}", brushId);
		}

		public static void MAPWriteSolid(StreamWriter sw, vec3 bbmin, vec3 bbmax, string material)
		{	
			sw.Write("\n{\n");
			MAPWriteSide(sw, new vec3(bbmin.x, bbmax.y, bbmax.z), bbmax,                               new vec3(bbmax.x,bbmin.y,bbmax.z), new vec3(0, - 1, 0), material);
			MAPWriteSide(sw, new vec3(bbmin.x, bbmax.y, bbmax.z), new vec3(bbmin.x, bbmin.y, bbmax.z), bbmin, new vec3(0, 0, -1), material);
			MAPWriteSide(sw, new vec3(bbmax.x, bbmax.y, bbmin.z), new vec3(bbmax.x, bbmin.y, bbmin.z), new vec3(bbmax.x,bbmin.y,bbmax.z), new vec3(0, 0, -1), material);
			MAPWriteSide(sw, bbmax,                               new vec3(bbmin.x, bbmax.y, bbmax.z), new vec3(bbmin.x, bbmax.y, bbmin.z), new vec3(0, 0, -1), material);
			MAPWriteSide(sw, new vec3(bbmax.x, bbmin.y, bbmin.z), bbmin,                               new vec3(bbmin.x, bbmin.y, bbmax.z), new vec3(0, 0, -1), material);
			MAPWriteSide(sw, new vec3(bbmax.x, bbmin.y, bbmin.z), new vec3(bbmax.x, bbmax.y, bbmin.z), new vec3(bbmin.x, bbmax.y, bbmin.z), new vec3(-1, 0, 0), material);
			sw.Write("}\n");
		}
		public static void MAPWriteSide(StreamWriter sw, vec3 v1, vec3 v2, vec3 v3, vec3 vaxis, string material)
		{

			vec3 uaxis = new vec3(v2.x - v1.x, v2.y - v1.y, 0);
			uaxis.Normalize();
			sw.Write("( " + v1.ToString() + " ) ( " + v2.ToString() + " ) ( " + v3.ToString() + " ) ");
			sw.Write(material);
			sw.Write(" [ " + uaxis.ToString() + " 0 ] ");
			sw.Write(" [ " + vaxis.ToString() + " 0 ] ");
			sw.Write(" 0 1.0 1.0\n");
		}
	}


	class Polygon
	{
		public vec3[] verts;
		public vec3 At(int i)
		{
			return verts[(i + verts.Length) % verts.Length];
		}
		public Polygon Copy(int i, int j, int add)
		{
			while (j < i) j += verts.Length;
			Polygon p = new Polygon();
			p.verts = new vec3[j - i + 1 + add];
			for(int k=0; i<=j; ++i,k++)
			{
				p.verts[k] = At(i);
			}
			return p;
		}

		public Polygon CollinearSimplify(double tolerance)
		{
			if (verts.Length <= 3)
				return this;
			
			List<vec3> list = new List<vec3>();
			for (int i = 0; i < verts.Length; i++)
			{
				vec3 current = At(i);
				//vec3 prev = (At(i - 1) - current).Normalize();
				//vec3 next = (At(i + 1) - current).Normalize();

				//if (vec3.Dot(prev, next) >= -1+(tolerance*0.05))
				//	continue;
				//if (Math.Atan2(prev.x * next.z - prev.z * next.x, prev.x * next.x + prev.z * next.z) < 0)
				//	continue;

				vec3 prev = At(i - 1);
				vec3 next = At(i + 1);

				if (Math.Abs(Utils.Area(prev, current, next)) <= tolerance)
					continue;
				list.Add(current);
			}

			Polygon p = new Polygon();
			p.verts = list.ToArray();
			return p;
		}

		public void ForceCCW()
		{
			if(SignedArea()<0)
				verts = verts.Reverse().ToArray();
		}

		double SignedArea()
		{
			double r = 0;
			for(int i = 0; i < verts.Length; i++)
			{
				int j = (i + 1) % verts.Length;
				vec3 v1 = verts[i];
				vec3 v2 = verts[j];

				r += (v1.x * v2.z);
				r -= (v1.z * v2.x);
			}
			return r/2.0;
		}
	}

	struct OsmNode
	{
		public long id;
		public double lat;
		public double lon;
	}

	struct OsmBuilding
	{
		public long id;
		public long[] nodeIds;
		public float height;
		public float min_height;
		public float ele;
		public int levels;
		public bool outline;
		public bool part;

		//public Rect2D bbox;
	}

	struct OsmRelation
	{
		bool building;

	}

	class OsmChunk
	{
		public Rect2D bounds;// = new Rect2D();
		public Dictionary<long, OsmNode> nodes = new Dictionary<long, OsmNode>();
		public List<OsmBuilding> buildings = new List<OsmBuilding>();
		//public List<OsmRelation> relations = new List<OsmRelation>();
		public float levelHeight = 3;

		double centerLon;
		double centerLat;
		vec3 b;

		public bool Load(string filename)
		{
			XmlDocument osm = new XmlDocument();
			osm.Load(filename);

			XmlElement root = osm.DocumentElement;
			Console.Out.WriteLine("version " + root.Attributes.GetNamedItem("version").InnerText);
			
			XmlNodeList boundsl = root.GetElementsByTagName("bounds");
			if (boundsl.Count != 1)
			{
				Console.Out.WriteLine("bounds node count " + boundsl.Count);
				return true;
			}
			else
			{
				double minlat = double.Parse(boundsl[0].Attributes.GetNamedItem("minlat").Value, NumberFormatInfo.InvariantInfo);
				double maxlat = double.Parse(boundsl[0].Attributes.GetNamedItem("maxlat").Value, NumberFormatInfo.InvariantInfo);
				double minlon = double.Parse(boundsl[0].Attributes.GetNamedItem("minlon").Value, NumberFormatInfo.InvariantInfo);
				double maxlon = double.Parse(boundsl[0].Attributes.GetNamedItem("maxlon").Value, NumberFormatInfo.InvariantInfo);

				bounds = new Rect2D(minlon, minlat, maxlon - minlon, maxlat - minlat);
				centerLon = bounds.x + bounds.w * 0.5;
				centerLat = bounds.y + bounds.h * 0.5;
				b = Utils.LonLatToXYZ(centerLon, centerLat);

				Console.Out.WriteLine("Bounds: (" + bounds.x + " x " + bounds.y + ") (" + bounds.w + " x " + bounds.h + ")");
			}

			foreach (XmlNode n in root)
			{
				//Console.Out.WriteLine("node name " + n.Name);
				if (n.Name == "node")
				{
					if (n.Attributes.Count > 0)
					{
						long id = long.Parse(n.Attributes.GetNamedItem("id").Value);
						OsmNode tNode = new OsmNode();
						tNode.id = id;
						tNode.lat = double.Parse(n.Attributes.GetNamedItem("lat").Value, NumberFormatInfo.InvariantInfo);
						tNode.lon = double.Parse(n.Attributes.GetNamedItem("lon").Value, NumberFormatInfo.InvariantInfo);

						nodes[id] = tNode;
					}
				}
				else if (n.Name == "way")
				{
					long id = long.Parse(n.Attributes.GetNamedItem("id").Value);
					bool building = false;
					bool buildingPart = false;
					float height = 0;
					float min_height = 0;
					float ele=0;
					int levels = 0;
					List<long> refIds = new List<long>();
					foreach (XmlNode cn in n)
					{
						if (cn.Name == "tag")
						{
							//<tag k="building" v="yes" />
							//<tag k="building" v="commercial"/>
							string k = cn.Attributes.GetNamedItem("k").Value;
							if (k == "building")
							{
								building = true;
								//cn.Attributes.GetNamedItem("v").Value;
							}
							//<tag k="building:part" v="yes"/>
							else if (k == "building:part")
							{
								buildingPart = true;
							}
							//<tag k="height" v="160"/>
							else if (k == "height")
							{
								height = float.Parse(cn.Attributes.GetNamedItem("v").Value, NumberFormatInfo.InvariantInfo);
							}
							else if (k == "min_height")
							{
								min_height = float.Parse(cn.Attributes.GetNamedItem("v").Value, NumberFormatInfo.InvariantInfo);
							}
							else if (k == "ele")
							{
								ele = float.Parse(cn.Attributes.GetNamedItem("v").Value, NumberFormatInfo.InvariantInfo);
							}
							//<tag k="building:levels" v="6"/>
							//<tag k="building:levels" v="13C"/>
							//<tag k="building:levels" v="1, 1M, 2M, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25"/>
							else if (k == "building:levels")
							{
								try
								{
									levels = int.Parse(cn.Attributes.GetNamedItem("v").Value);
								}
								catch { }
							}
							//<tag k="highway" v="secondary"/>
							//<tag k="highway" v="pedestrian"/>
							//<tag k="building:colour" v="#C7C2AF"/>
							//<tag k="building:material" v="glass"/>
							//<tag k="roof:material" v="concrete"/>
							//<tag k="roof:shape" v="flat"/>
						}
						else if (cn.Name == "nd")
						{
							long refId = long.Parse(cn.Attributes.GetNamedItem("ref").Value);
							refIds.Add(refId);
						}
					}
					if (building || buildingPart)
					{
						//Console.Out.WriteLine("building: id " + id + " height " + height + " levels " + levels + " nodes count " + refIds.Count);

						if (levels == 0)
							levels = 1;
						if (height == 0)
							height = levels * levelHeight;

						OsmBuilding bd = new OsmBuilding();
						bd.id = id;
						bd.levels = levels;
						bd.height = height;
						bd.min_height = min_height;
						bd.ele = ele;
						bd.nodeIds = refIds.ToArray();
						bd.part = buildingPart;
						buildings.Add(bd);
					}
				}
				else if (n.Name == "relation")
				{
					bool building=false;
					List<long> partIds = new List<long>();
					long outlineId = -1;

					foreach (XmlNode cn in n)
					{
						if (cn.Name == "tag")
						{
							string k = cn.Attributes.GetNamedItem("k").Value;
							if (k == "type")
							{
								if (cn.Attributes.GetNamedItem("v").Value == "building")
									building = true;
							}
							//else if (k == "name")
							//{
							//	string name = cn.Attributes.GetNamedItem("v").Value;
							//}
						}
						else if (cn.Name == "member")
						{
							string type = cn.Attributes.GetNamedItem("type").Value;
							if(type == "way") {
								string role = cn.Attributes.GetNamedItem("role").Value;
								long refId = long.Parse(cn.Attributes.GetNamedItem("ref").Value);
								if (role == "outline")
								{
									outlineId = refId;
								}
								else if (role == "part")
								{
									partIds.Add(refId);
								}
							}
						}
					}
					if (building) {
						for (int i = 0; i < buildings.Count; i++)
						{
							if (buildings[i].id == outlineId) {
								OsmBuilding b = buildings[i];
								if (!b.part)
								{
									b.outline = true;
									if (b.ele > 0)
										b.height = b.ele;
									else
										b.height = levelHeight;
									buildings[i] = b;
								}
								break;
							}
						}
					}
				}
			}
			Console.Out.WriteLine("Node count " + nodes.Count);
			Console.Out.WriteLine("Building count " + buildings.Count);

			return false;
		}

		public void Cull(Rect2D bounding)
		{
			for (int i = 0; i < buildings.Count; i++)
			{
				Rect2D bb = GetBuildingRect(buildings[i]);
				if (!bounding.Contains(bb))
				{
					buildings.Remove(buildings[i]);
					i--;
				}
			}
		}

		public Rect2D GetBuildingRect(OsmBuilding b)
		{
			double x1 = Double.MaxValue;
			double y1 = Double.MaxValue;
			double x2 = Double.MinValue;
			double y2 = Double.MinValue;

			for (int i = 0; i < b.nodeIds.Length; i++) {
				vec3 v = GetNodeCoords(nodes[b.nodeIds[i]]);

				x1 = Math.Min(x1, v.x);
				y1 = Math.Min(y1, v.z);
				x2 = Math.Max(x2, v.x);
				y2 = Math.Max(y2, v.z);
			}

			return new Rect2D(x1,y1,x2 - x1, y2 - y1);
		}


		public List<Polygon> ConvexPartition(Polygon p)
		{

			List<Polygon> list = new List<Polygon>();

			if (p.verts.Length < 3)
				return list;

			p.ForceCCW();

			vec3 lowerInt = new vec3();
			int lowerIndex = 0;
			vec3 upperInt = new vec3();
			int upperIndex = 0;

			for (int i = 0; i < p.verts.Length; i++)
			{
				vec3 prev = p.At(i - 1);
				vec3 on = p.At(i);
				vec3 next = p.At(i + 1);

				if (Utils.Reflex(prev, on, next))
				{
					double lowerDist = Double.MaxValue;
					double upperDist = Double.MaxValue;
					for (int j = 0; j < p.verts.Length; j++)
					{
						if ((i == j) || (i == ((j + 1) % p.verts.Length)) || (i == (j - 1 + p.verts.Length) % p.verts.Length))
							continue;

						//incoming edge
						vec3 jSelf = p.At(j);
						vec3 jPrev = p.At(j - 1);

						bool leftOK = Utils.Area(prev, on, jSelf) > 0;
						bool rightOK = Utils.Area(prev, on, jPrev) < 0;

						//check collinear
						bool leftOnOK = Utils.IsCollinear(prev, on, jSelf);
						bool rightOnOK = Utils.IsCollinear(prev, on, jPrev);

						if(leftOnOK || rightOnOK)
						{
							double d = Utils.SqDist(on, jSelf);

							if(d<lowerDist)
							{
								lowerDist = d;
								lowerInt = jSelf;
								lowerIndex = j;
							}

							d = Utils.SqDist(on, jPrev);

							if (d < lowerDist)
							{
								lowerDist = d;
								lowerInt = jPrev;
								lowerIndex = j - 1;
							}
						}
						else if (leftOK && rightOK)//Intersection in-between
						{
							vec3 intersect = Utils.LineIntersect(p.At(i - 1), p.At(i), p.At(j), p.At(j - 1));

							if (Utils.Area(p.At(i + 1), p.At(i), intersect) < 0)//right
							{
								double d = Utils.SqDist(p.At(i), intersect);

								if (d < lowerDist)
								{
									lowerDist = d;
									lowerInt = intersect;
									lowerIndex = j;
								}
							}
						}

						//outgoing edge
						vec3 jNext = p.At(j + 1);

						bool leftOKn = Utils.Area(next, on, jNext) > 0;
						bool rightOKn = Utils.Area(next, on, jSelf) < 0;

						//check collinear
						bool leftOnOKn = Utils.IsCollinear(next, on, jNext);
						bool rightOnOKn = Utils.IsCollinear(next, on, jSelf);

						if (leftOnOKn || rightOnOKn)
						{
							double d = Utils.SqDist(on, jNext);

							if (d < upperDist)
							{
								upperDist = d;
								upperInt = jNext;
								upperIndex = j + 1;
							}

							d = Utils.SqDist(on, jSelf);

							if (d < upperDist)
							{
								upperDist = d;
								upperInt = jSelf;
								upperIndex = j;
							}
						}
						else if (leftOKn && rightOKn)//Intersection in-between
						{
							vec3 intersect = Utils.LineIntersect(p.At(i + 1), p.At(i), p.At(j), p.At(j + 1));

							if (Utils.Area(p.At(i - 1), p.At(i), intersect) > 0)//left
							{
								double d = Utils.SqDist(p.At(i), intersect);

								if (d < upperDist)
								{
									upperDist = d;
									upperIndex = j;
									upperInt = intersect;
								}
							}
						}
					}

					Polygon lowerPoly = null;
					Polygon upperPoly = null;
					if (lowerIndex == (upperIndex + 1) % p.verts.Length)
					{
						//middle point
						vec3 sp = ((lowerInt + upperInt) * 0.5f);

						lowerPoly = p.Copy(i, upperIndex, 1);
						lowerPoly.verts[lowerPoly.verts.Length - 1] = sp;
						upperPoly = p.Copy(lowerIndex, i, 1);
						upperPoly.verts[upperPoly.verts.Length - 1] = sp;
					}
					else
					{
						//find vertex
						double highest = 0;
						int bestIndex = lowerIndex;
						while (upperIndex < lowerIndex) upperIndex += p.verts.Length;

						for(int j = lowerIndex; j<= upperIndex; ++j)
						{
							if (Utils.CanSee(i, j, p))
							{
								double score = 1 / (Utils.SqDist(p.At(i), p.At(j)) + 1);

								vec3 prevj = p.At(j - 1);
								vec3 onj = p.At(j);
								vec3 nextj = p.At(j + 1);

								if (Utils.Reflex(prevj, onj, nextj))
								{
									if ((Utils.Area(prevj, onj, on) <= 0) && (Utils.Area(nextj, onj, on) >= 0))//RightOn LeftOn
										score += 3;
									else
										score += 2;
								}
								else
								{
									score += 1;
								}

								if(score> highest)
								{
									bestIndex = j;
									highest = score;
								}
							}
						}

						lowerPoly = p.Copy(i, bestIndex, 0);
						upperPoly = p.Copy(bestIndex, i, 0);
					}
					
					if (lowerPoly.verts.Length < upperPoly.verts.Length)
					{
						list.AddRange(ConvexPartition(lowerPoly));
						list.AddRange(ConvexPartition(upperPoly));
					}
					else
					{
						list.AddRange(ConvexPartition(upperPoly));
						list.AddRange(ConvexPartition(lowerPoly));
					}

					return list;
				}
			}

			list.Add(p);

			//collinear simplify
			for (int i = 0; i < list.Count; i++){
				list[i] = list[i].CollinearSimplify(0.3);
			}

			return list;
		}

		public Polygon[] GetBuildingPolys(OsmBuilding bd)
		{
			Polygon p = new Polygon();
			p.verts = new vec3[bd.nodeIds.Length - 1];
			for (int j = 0; j < bd.nodeIds.Length - 1; j++)
			{
				OsmNode n1 = nodes[bd.nodeIds[j]];
				vec3 v1 = GetNodeCoords(n1);
				p.verts[j] = v1;
			}
			p.ForceCCW();
			p = p.CollinearSimplify(0.3);

			//return new Polygon[] { p };

			return ConvexPartition(p).ToArray();
		}

		public vec3 GetNodeCoords(OsmNode n)
		{
			vec3 v = Utils.LonLatToXYZ(n.lon, n.lat);
			v -= b;
			Utils.RotateVec2(centerLon, ref v.x, ref v.z);
			Utils.RotateVec2((-90 + centerLat), ref v.y, ref v.z);
			Utils.RotateVec2(Settings.yAndgle, ref v.x, ref v.z);

			v.y = 0;

			return v;
		}
	}
}
