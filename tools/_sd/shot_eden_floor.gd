extends SceneTree

# 拍 eden 开场：相机被 limit_left=0 卡在 x[0..800]，
# 一帧就能同时看到 0~600 藤地板 + 600 之后的草地接缝。
func _initialize() -> void:
	var node: Node = load("res://eden.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	# 等进场黑幕淡完（HoldTime 0.12 + FadeTime 0.8）
	for i in range(90):
		await process_frame
	var img := root.get_viewport().get_texture().get_image()
	img.save_png("res://docs/screenshots/_eden_floor.png")
	var box: Node2D = node.get_node("box")
	print("[Shot] eden 出生点 x=%.0f 已截图" % box.global_position.x)
	quit()
