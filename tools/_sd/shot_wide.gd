extends SceneTree

# 拍一张广角图，能看全泥潭 1584 宽的范围。
# 临时把相机 zoom 拉到 0.5（视野翻倍），只为截图，不改场景文件。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	if box == null:
		print("[Shot] 找不到 box"); quit(); return

	box.global_position = Vector2(1750, 200)
	for i in range(100):
		await physics_frame

	var cam: Camera2D = box.get_node_or_null("Camera2D")
	if cam != null:
		cam.zoom = Vector2(0.5, 0.5)

	for i in range(6):
		await process_frame

	var img := root.get_viewport().get_texture().get_image()
	img.save_png("res://docs/screenshots/mud_wide.png")
	print("[Shot] 广角图完成 ", img.get_size())
	quit()
