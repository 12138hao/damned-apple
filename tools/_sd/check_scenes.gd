extends SceneTree

# 只做加载自检：eden 和 jungle 能不能实例化、有没有脚本报错
func _initialize() -> void:
	for path in ["res://eden.tscn", "res://jungle.tscn"]:
		var ps: PackedScene = load(path)
		if ps == null:
			print("[Check] 加载失败: ", path)
			continue
		var n: Node = ps.instantiate()
		root.add_child(n)
		await process_frame
		await process_frame
		print("[Check] ok %s  子节点=%d" % [path, n.get_child_count()])
		n.queue_free()
		await process_frame
	print("[Check] 全部完成")
	quit()
