extends SceneTree

# 只抓开场第 4 帧：SceneFadeIn 应该还是全黑
func _initialize() -> void:
	var node: Node = load("res://eden.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	for i in range(4):
		await process_frame
	root.get_viewport().get_texture().get_image().save_png("res://docs/screenshots/_fade_check.png")
	print("[Check] 第 4 帧已截图")
	quit()
