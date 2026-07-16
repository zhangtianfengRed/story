#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HexWarriors.EditorTools
{
	public sealed class ConfigTableConverterWindow : EditorWindow
	{
		private const string WindowMenuPath = "Tools/Configs/运行时 JSON 导出工具";
		private const float DropAreaHeight = 96f;

		private readonly List<string> _selectedPaths = new();
		private Vector2 _scrollPosition;
		private string _statusMessage = "请拖入 .xlsx 表格文件，然后点击“导出 JSON”。";

		[MenuItem(WindowMenuPath)]
		public static void OpenWindow()
		{
			var window = GetWindow<ConfigTableConverterWindow>("运行时 JSON 导出");
			window.minSize = new Vector2(620f, 420f);
			window.Show();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("运行时 JSON 导出工具", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"只支持 XLSX -> Runtime JSON。第 1 行为字段名，第 2 行为字段说明，第 3 行起为数据。导出时会按 xlsx 文件名查找并写入 Assets/Configs/Runtime 下的同名数组 JSON；缺少目录或文件时会自动创建。",
				MessageType.Info);

			EditorGUILayout.Space(6f);
			DrawDropArea();
			EditorGUILayout.Space(6f);
			DrawSelectedFiles();
			EditorGUILayout.Space(6f);
			DrawActions();
			EditorGUILayout.Space(6f);
			EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
		}

		private void DrawDropArea()
		{
			var rect = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
			GUI.Box(rect, "将 .xlsx 表格文件拖到这里", EditorStyles.helpBox);

			var currentEvent = Event.current;
			if (!rect.Contains(currentEvent.mousePosition))
			{
				return;
			}

			if (currentEvent.type != EventType.DragUpdated && currentEvent.type != EventType.DragPerform)
			{
				return;
			}

			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			if (currentEvent.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				AddDraggedPaths();
			}

			currentEvent.Use();
		}

		private void DrawSelectedFiles()
		{
			EditorGUILayout.LabelField($"已选择表格 ({_selectedPaths.Count})", EditorStyles.boldLabel);
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.MinHeight(150f));
			if (_selectedPaths.Count == 0)
			{
				EditorGUILayout.LabelField("尚未选择 xlsx 文件。", EditorStyles.miniLabel);
			}
			else
			{
				for (var i = 0; i < _selectedPaths.Count; i++)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.LabelField(_selectedPaths[i], EditorStyles.wordWrappedMiniLabel);
						if (GUILayout.Button("移除", GUILayout.Width(72f)))
						{
							_selectedPaths.RemoveAt(i);
							i--;
						}
					}
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawActions()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(_selectedPaths.Count == 0))
				{
					if (GUILayout.Button("导出 JSON", GUILayout.Height(30f)))
					{
						ExportSelectedFiles();
					}
				}

				if (GUILayout.Button("清空", GUILayout.Width(96f), GUILayout.Height(30f)))
				{
					_selectedPaths.Clear();
					_statusMessage = "已清空选择。";
				}
			}
		}

		private void AddDraggedPaths()
		{
			foreach (var path in DragAndDrop.paths)
			{
				AddPath(path);
			}

			foreach (var reference in DragAndDrop.objectReferences)
			{
				if (reference == null)
				{
					continue;
				}

				AddPath(AssetDatabase.GetAssetPath(reference));
			}

			_statusMessage = $"已选择 {_selectedPaths.Count} 个 xlsx 文件。";
			Repaint();
		}

		private void AddPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			var normalizedPath = path.Replace('\\', '/');
			if (!string.Equals(Path.GetExtension(normalizedPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (!_selectedPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
			{
				_selectedPaths.Add(normalizedPath);
			}
		}

		private void ExportSelectedFiles()
		{
			var reports = new List<ConfigTableExportReport>();
			foreach (var selectedPath in _selectedPaths.ToArray())
			{
				try
				{
					reports.Add(ConfigTableRuntimeJsonExporter.Export(selectedPath));
				}
				catch (Exception exception)
				{
					reports.Add(ConfigTableExportReport.Failed(selectedPath, exception.Message));
					Debug.LogError($"[ConfigTableExporter] path={selectedPath} status=failed reason={exception}");
				}
			}

			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			var summary = string.Join(Environment.NewLine, reports.Select(report => report.Message));
			_statusMessage = summary;
			Debug.Log($"[ConfigTableExporter] complete{Environment.NewLine}{summary}");
			EditorUtility.DisplayDialog("运行时 JSON 导出", summary, "确定");
		}
	}

	internal static class ConfigTableRuntimeJsonExporter
	{
		private const string RuntimeRoot = "Assets/Configs/Runtime";
		private static readonly UTF8Encoding Utf8NoBom = new(false);

		public static ConfigTableExportReport Export(string inputPath)
		{
			var absoluteInputPath = ResolveInputAbsolutePath(inputPath);
			if (!File.Exists(absoluteInputPath))
			{
				throw new FileNotFoundException($"找不到输入表格：{inputPath}", inputPath);
			}

			if (!string.Equals(Path.GetExtension(absoluteInputPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("当前导出工具只支持 .xlsx 表格文件。");
			}

			var datasetName = Path.GetFileNameWithoutExtension(absoluteInputPath);
			var runtimeJsonAssetPath = ResolveRuntimeJsonAssetPath(datasetName);
			var table = MinimalXlsx.ReadFirstWorksheet(absoluteInputPath);
			var rows = BuildJsonRows(table, absoluteInputPath);
			ValidateExistingRuntimeJsonIsDataArray(runtimeJsonAssetPath);

			File.WriteAllText(ToAbsoluteAssetPath(runtimeJsonAssetPath), JsonConvert.SerializeObject(rows, Formatting.Indented), Utf8NoBom);
			AssetDatabase.ImportAsset(runtimeJsonAssetPath, ImportAssetOptions.ForceSynchronousImport);

			return ConfigTableExportReport.Success(inputPath, $"已导出 {Path.GetFileName(inputPath)}，写入 {runtimeJsonAssetPath}，数据 {rows.Count} 行。");
		}

		private static JArray BuildJsonRows(IReadOnlyList<IReadOnlyList<string>> table, string sourcePath)
		{
			if (table.Count < 1)
			{
				throw new InvalidOperationException($"表格“{sourcePath}”没有可读取的行。");
			}

			var fields = ParseFieldNames(table[0]);
			var dataRows = new JArray();
			foreach (var row in table.Skip(2))
			{
				if (row == null || row.All(string.IsNullOrWhiteSpace))
				{
					continue;
				}

				var jsonRow = new JObject();
				for (var i = 0; i < fields.Count; i++)
				{
					var value = i < row.Count ? row[i] ?? string.Empty : string.Empty;
					jsonRow[fields[i]] = value;
				}

				dataRows.Add(jsonRow);
			}

			return dataRows;
		}

		private static List<string> ParseFieldNames(IReadOnlyList<string> headerCells)
		{
			var lastFieldIndex = -1;
			for (var i = 0; i < headerCells.Count; i++)
			{
				if (!string.IsNullOrWhiteSpace(headerCells[i]))
				{
					lastFieldIndex = i;
				}
			}

			if (lastFieldIndex < 0)
			{
				throw new InvalidOperationException("表格第 1 行没有字段名。");
			}

			var fields = new List<string>();
			var used = new HashSet<string>(StringComparer.Ordinal);
			for (var i = 0; i <= lastFieldIndex; i++)
			{
				var fieldName = headerCells[i]?.Trim();
				if (string.IsNullOrWhiteSpace(fieldName))
				{
					throw new InvalidOperationException($"第 {i + 1} 列字段名为空。字段名必须从第一列开始连续填写。");
				}

				if (!used.Add(fieldName))
				{
					throw new InvalidOperationException($"字段名重复：{fieldName}");
				}

				fields.Add(fieldName);
			}

			return fields;
		}

		private static void ValidateExistingRuntimeJsonIsDataArray(string runtimeJsonAssetPath)
		{
			var absolutePath = ToAbsoluteAssetPath(runtimeJsonAssetPath);
			if (!File.Exists(absolutePath))
			{
				return;
			}

			var existing = JToken.Parse(File.ReadAllText(absolutePath, Encoding.UTF8));
			if (existing is JArray)
			{
				return;
			}

			throw new InvalidOperationException($"Runtime JSON“{runtimeJsonAssetPath}”不是数组数据文件，已停止替换。");
		}

		private static string ResolveRuntimeJsonAssetPath(string datasetName)
		{
			var runtimeRootAbsolutePath = ToAbsoluteAssetPath(RuntimeRoot);
			if (!Directory.Exists(runtimeRootAbsolutePath))
			{
				Directory.CreateDirectory(runtimeRootAbsolutePath);
			}

			var matches = Directory
				.GetFiles(runtimeRootAbsolutePath, datasetName + ".json", SearchOption.AllDirectories)
				.Select(ToAssetPath)
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToList();
			if (matches.Count == 0)
			{
				return $"{RuntimeRoot}/{datasetName}.json";
			}

			if (matches.Count > 1)
			{
				throw new InvalidOperationException($"Runtime 目录下找到多个同名 JSON：{string.Join(", ", matches)}");
			}

			return matches[0];
		}

		private static string ResolveInputAbsolutePath(string inputPath)
		{
			if (string.IsNullOrWhiteSpace(inputPath))
			{
				throw new ArgumentException("输入路径为空。", nameof(inputPath));
			}

			var normalizedPath = inputPath.Replace('\\', '/');
			return normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
				? ToAbsoluteAssetPath(normalizedPath)
				: Path.GetFullPath(inputPath);
		}

		private static string ToAbsoluteAssetPath(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException($"资源路径“{assetPath}”必须以 Assets/ 开头。", nameof(assetPath));
			}

			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
			if (string.IsNullOrWhiteSpace(projectRoot))
			{
				throw new InvalidOperationException("无法解析 Unity 工程根目录。");
			}

			var relativePath = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
			return Path.Combine(projectRoot, "Assets", relativePath);
		}

		private static string ToAssetPath(string absolutePath)
		{
			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
			if (string.IsNullOrWhiteSpace(projectRoot))
			{
				throw new InvalidOperationException("无法解析 Unity 工程根目录。");
			}

			var assetsRoot = Path.Combine(projectRoot, "Assets");
			var relativePath = Path.GetRelativePath(assetsRoot, absolutePath).Replace('\\', '/');
			return "Assets/" + relativePath;
		}
	}

	internal sealed class ConfigTableExportReport
	{
		private ConfigTableExportReport(string inputPath, string status, string message)
		{
			InputPath = inputPath;
			Status = status;
			Message = $"[{ResolveStatusLabel(status)}] {message}";
		}

		public string InputPath { get; }

		public string Status { get; }

		public string Message { get; }

		public static ConfigTableExportReport Success(string inputPath, string message)
		{
			return new ConfigTableExportReport(inputPath, "OK", message);
		}

		public static ConfigTableExportReport Failed(string inputPath, string message)
		{
			return new ConfigTableExportReport(inputPath, "FAIL", message);
		}

		private static string ResolveStatusLabel(string status)
		{
			return status switch
			{
				"OK" => "成功",
				"FAIL" => "失败",
				_ => status
			};
		}
	}

	internal static class MinimalXlsx
	{
		private static readonly UTF8Encoding Utf8NoBom = new(false);

		public static List<List<string>> ReadFirstWorksheet(string absolutePath)
		{
			using var fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
			var sharedStrings = ReadSharedStrings(archive);
			var worksheetPath = ResolveFirstWorksheetPath(archive);
			var worksheetEntry = archive.GetEntry(worksheetPath);
			if (worksheetEntry == null)
			{
				throw new InvalidOperationException($"在“{absolutePath}”中找不到工作表数据“{worksheetPath}”。");
			}

			using var worksheetStream = worksheetEntry.Open();
			var document = XDocument.Load(worksheetStream);
			var sheetData = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "sheetData");
			if (sheetData == null)
			{
				return new List<List<string>>();
			}

			var rows = new List<List<string>>();
			var nextRowIndex = 0;
			foreach (var rowElement in sheetData.Elements().Where(element => element.Name.LocalName == "row"))
			{
				var rowIndex = TryGetRowIndex(rowElement, out var resolvedRowIndex) ? resolvedRowIndex : nextRowIndex;
				while (rows.Count < rowIndex)
				{
					rows.Add(new List<string>());
				}

				var rowValues = new List<string>();
				var sequentialColumnIndex = 0;
				foreach (var cellElement in rowElement.Elements().Where(element => element.Name.LocalName == "c"))
				{
					var columnIndex = TryGetCellColumnIndex(cellElement, out var resolvedColumnIndex)
						? resolvedColumnIndex
						: sequentialColumnIndex;
					while (rowValues.Count <= columnIndex)
					{
						rowValues.Add(string.Empty);
					}

					rowValues[columnIndex] = ReadCellValue(cellElement, sharedStrings);
					sequentialColumnIndex = columnIndex + 1;
				}

				rows.Add(rowValues);
				nextRowIndex = rowIndex + 1;
			}

			while (rows.Count > 0 && rows[rows.Count - 1].All(string.IsNullOrWhiteSpace))
			{
				rows.RemoveAt(rows.Count - 1);
			}

			return rows;
		}

		public static void WriteSingleWorksheet(string absolutePath, string sheetName, IReadOnlyList<IReadOnlyList<string>> rows)
		{
			var directory = Path.GetDirectoryName(absolutePath);
			if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
			using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
			WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
			WriteEntry(archive, "_rels/.rels", BuildPackageRelationshipsXml());
			WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml(sheetName));
			WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
			WriteEntry(archive, "xl/styles.xml", BuildStylesXml());
			WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
		}

		private static void WriteEntry(ZipArchive archive, string path, string content)
		{
			var entry = archive.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
			using var writer = new StreamWriter(entry.Open(), Utf8NoBom);
			writer.Write(content);
		}

		private static List<string> ReadSharedStrings(ZipArchive archive)
		{
			var entry = archive.GetEntry("xl/sharedStrings.xml");
			if (entry == null)
			{
				return new List<string>();
			}

			using var stream = entry.Open();
			var document = XDocument.Load(stream);
			return document
				.Descendants()
				.Where(element => element.Name.LocalName == "si")
				.Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)))
				.ToList();
		}

		private static string ResolveFirstWorksheetPath(ZipArchive archive)
		{
			var workbookEntry = archive.GetEntry("xl/workbook.xml");
			var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
			if (workbookEntry == null || relationshipsEntry == null)
			{
				if (archive.GetEntry("xl/worksheets/sheet1.xml") != null)
				{
					return "xl/worksheets/sheet1.xml";
				}

				throw new InvalidOperationException("XLSX 工作簿元数据缺失。");
			}

			using var workbookStream = workbookEntry.Open();
			var workbook = XDocument.Load(workbookStream);
			var firstSheet = workbook.Descendants().FirstOrDefault(element => element.Name.LocalName == "sheet");
			var relationshipId = firstSheet?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value;
			if (string.IsNullOrWhiteSpace(relationshipId))
			{
				throw new InvalidOperationException("XLSX 工作簿没有可用的第一个工作表关系 ID。");
			}

			using var relationshipsStream = relationshipsEntry.Open();
			var relationships = XDocument.Load(relationshipsStream);
			var target = relationships
				.Descendants()
				.FirstOrDefault(element =>
					element.Name.LocalName == "Relationship" &&
					string.Equals(element.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal))
				?.Attribute("Target")
				?.Value;
			if (string.IsNullOrWhiteSpace(target))
			{
				throw new InvalidOperationException($"XLSX 关系“{relationshipId}”没有解析到工作表目标。");
			}

			return NormalizeWorkbookTarget(target);
		}

		private static string NormalizeWorkbookTarget(string target)
		{
			var normalized = target.Replace('\\', '/');
			if (normalized.StartsWith("/", StringComparison.Ordinal))
			{
				return normalized.TrimStart('/');
			}

			if (normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
			{
				return normalized;
			}

			return "xl/" + normalized.TrimStart('/');
		}

		private static bool TryGetCellColumnIndex(XElement cellElement, out int columnIndex)
		{
			columnIndex = 0;
			var reference = cellElement.Attribute("r")?.Value;
			if (string.IsNullOrWhiteSpace(reference))
			{
				return false;
			}

			var index = 0;
			var hasLetters = false;
			foreach (var character in reference)
			{
				if (!char.IsLetter(character))
				{
					break;
				}

				hasLetters = true;
				index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
			}

			if (!hasLetters)
			{
				return false;
			}

			columnIndex = index - 1;
			return columnIndex >= 0;
		}

		private static bool TryGetRowIndex(XElement rowElement, out int rowIndex)
		{
			rowIndex = 0;
			var reference = rowElement.Attribute("r")?.Value;
			if (!int.TryParse(reference, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBasedRowIndex))
			{
				return false;
			}

			rowIndex = oneBasedRowIndex - 1;
			return rowIndex >= 0;
		}

		private static string ReadCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
		{
			var cellType = cellElement.Attribute("t")?.Value;
			if (string.Equals(cellType, "inlineStr", StringComparison.Ordinal))
			{
				return string.Concat(cellElement.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
			}

			var rawValue = cellElement.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? string.Empty;
			if (string.Equals(cellType, "s", StringComparison.Ordinal))
			{
				return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex) &&
					sharedIndex >= 0 &&
					sharedIndex < sharedStrings.Count
					? sharedStrings[sharedIndex]
					: string.Empty;
			}

			if (string.Equals(cellType, "b", StringComparison.Ordinal))
			{
				return rawValue == "1" ? "true" : "false";
			}

			return rawValue;
		}

		private static string BuildContentTypesXml()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
				"<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
				"<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
				"<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
				"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
				"<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
				"</Types>";
		}

		private static string BuildPackageRelationshipsXml()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
				"</Relationships>";
		}

		private static string BuildWorkbookXml(string sheetName)
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
				"<sheets>" +
				$"<sheet name=\"{EscapeXmlAttribute(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/>" +
				"</sheets>" +
				"</workbook>";
		}

		private static string BuildWorkbookRelationshipsXml()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
				"<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
				"</Relationships>";
		}

		private static string BuildStylesXml()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
				"<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
				"<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
				"<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
				"<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
				"<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
				"</styleSheet>";
		}

		private static string BuildWorksheetXml(IReadOnlyList<IReadOnlyList<string>> rows)
		{
			var builder = new StringBuilder();
			builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
			builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
			builder.Append("<sheetData>");
			for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var oneBasedRow = rowIndex + 1;
				builder.AppendFormat(CultureInfo.InvariantCulture, "<row r=\"{0}\">", oneBasedRow);
				var row = rows[rowIndex] ?? (IReadOnlyList<string>)Array.Empty<string>();
				for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
				{
					var cellReference = GetCellReference(columnIndex, oneBasedRow);
					var value = row[columnIndex] ?? string.Empty;
					var preserveSpace = value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]));
					builder.AppendFormat(CultureInfo.InvariantCulture, "<c r=\"{0}\" t=\"inlineStr\"><is><t", cellReference);
					if (preserveSpace)
					{
						builder.Append(" xml:space=\"preserve\"");
					}

					builder.Append(">");
					builder.Append(EscapeXmlText(value));
					builder.Append("</t></is></c>");
				}

				builder.Append("</row>");
			}

			builder.Append("</sheetData>");
			builder.Append("</worksheet>");
			return builder.ToString();
		}

		private static string GetCellReference(int zeroBasedColumnIndex, int oneBasedRowIndex)
		{
			return GetColumnName(zeroBasedColumnIndex) + oneBasedRowIndex.ToString(CultureInfo.InvariantCulture);
		}

		private static string GetColumnName(int zeroBasedColumnIndex)
		{
			var dividend = zeroBasedColumnIndex + 1;
			var columnName = string.Empty;
			while (dividend > 0)
			{
				var modulo = (dividend - 1) % 26;
				columnName = (char)('A' + modulo) + columnName;
				dividend = (dividend - modulo) / 26;
			}

			return columnName;
		}

		private static string EscapeXmlText(string value)
		{
			return (value ?? string.Empty)
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;");
		}

		private static string EscapeXmlAttribute(string value)
		{
			return EscapeXmlText(value)
				.Replace("\"", "&quot;")
				.Replace("'", "&apos;");
		}
	}
}
#endif
