extends SceneTree

# 模组DLL独立加载；本项目无自有场景/贴图，资源包只需清单与本地化。
# 使用Godot原生PCKPacker，避免资源包导出器额外发布一个.NET应用。
func _initialize():
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Expected project directory and output PCK")
		quit(1)
		return
	var project_dir: String = args[0]
	var output_path: String = args[1]
	var packer := PCKPacker.new()
	if packer.pck_start(output_path) != OK:
		quit(1)
		return
	var paths: Array[String] = ["IndiumStar.json"]
	for language in ["eng", "zhs"]:
		var language_path: String = "IndiumStar/localization/" + language
		for filename in DirAccess.get_files_at(project_dir.path_join(language_path)):
			if filename.ends_with(".json"):
				paths.append(language_path.path_join(filename))
	for path in paths:
		if packer.add_file("res://" + path, project_dir.path_join(path)) != OK:
			push_error("Packing failed: " + path)
			quit(1)
			return
	if packer.flush() != OK or not ProjectSettings.load_resource_pack(output_path, true):
		push_error("PCK creation/loading failed")
		quit(1)
		return
	for path in paths:
		var packed := FileAccess.get_file_as_string("res://" + path)
		var original := FileAccess.get_file_as_string(project_dir.path_join(path))
		if packed != original or JSON.parse_string(packed) == null:
			push_error("PCK roundtrip mismatch: " + path)
			quit(1)
			return
	print("PASS native PCK roundtrip: ", paths.size(), " JSON files; no build/staging duplicates")
	quit(0)
