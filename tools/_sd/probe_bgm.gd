extends SceneTree

# 进场景后确认 BGM 播放器真的在放，并且放的就是该放的那首
# 用法：-- --scene=jungle

func _initialize() -> void:
	var scene := "jungle"
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--scene="): scene = a.substr(8)

	var node: Node = load("res://%s.tscn" % scene).instantiate()
	root.add_child(node)
	current_scene = node
	for i in range(40):
		await process_frame

	var am: Node = root.get_node_or_null("AudioManager")
	if am == null:
		print("!! 没找到 AudioManager"); quit(); return

	var found := 0
	for c in am.get_children():
		if c is AudioStreamPlayer and c.playing:
			var path: String = c.stream.resource_path if c.stream else "?"
			print("  >> 正在播放: %-40s 时长 %.1f 秒" % [path, c.stream.get_length() if c.stream else 0.0])
			found += 1
	if found == 0:
		print("  XX 没有播放器在放（BGM 没起来）")
	quit()
