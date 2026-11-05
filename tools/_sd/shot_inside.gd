extends SceneTree

# 拍一张 jungleinside 的全屏画面，看摄像机框住的是不是房间正中。

func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	var cam: Camera2D = box.get_node("Camera2D")
	var room: Sprite2D = node.get_node("Sprite2D")

	# 把玩家放回出生点，别让镜头因为人跑远而偏
	box.global_position = Vector2(223.9, 252.6)
	for i in range(30):
		await process_frame

	var tex_size: Vector2 = room.texture.get_size() * room.scale
	var center: Vector2 = room.global_position
	print("房间贴图 世界尺寸=%s  中心=%s" % [tex_size, center])
	print("摄像机 zoom=%s  视口世界尺寸=%s" % [cam.zoom, Vector2(800, 450) / cam.zoom])
	print("limits L=%s T=%s R=%s B=%s" % [cam.limit_left, cam.limit_top, cam.limit_right, cam.limit_bottom])
	print("摄像机实际中心=%s  （屏幕中心对应的世界坐标）" % cam.get_screen_center_position())

	var img := root.get_viewport().get_texture().get_image()
	if img == null:
		print("!! 拿不到视口"); quit(); return
	img.save_png("res://docs/screenshots/jungleinside_cam.png")
	print("[Shot] 完成 ", img.get_size())
	quit()
