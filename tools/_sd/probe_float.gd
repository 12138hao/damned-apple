extends SceneTree

# 验证三件事：
#   ① 泥浆有没有真的在动（截两帧对比）
#   ② 跳跃会不会溅泥
#   ③ 泥块颜色和泥浆对不对得上

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var box: CharacterBody2D = node.get_node_or_null("box")
	if box == null:
		print("[Probe] 找不到 box"); quit(); return

	# 站进泥潭，等它稳定
	box.global_position = Vector2(1500, 330)
	for i in range(90):
		await physics_frame
	print("[Probe] 站位 y=%.1f 在地面=%s" % [box.global_position.y, box.is_on_floor()])

	# ① 两帧对比：间隔 0.5 秒
	var img1 := root.get_viewport().get_texture().get_image()
	img1.save_png("res://tools/_sd/frame1.png")
	for i in range(30):
		await physics_frame
	var img2 := root.get_viewport().get_texture().get_image()
	img2.save_png("res://tools/_sd/frame2.png")
	print("[Probe] 两帧已保存")

	# ② 跳跃：按住 jump 截一帧，看泥点有没有喷出来
	Input.action_press("jump")
	for i in range(7):
		await physics_frame
	var img3 := root.get_viewport().get_texture().get_image()
	img3.save_png("res://tools/_sd/jump.png")
	Input.action_release("jump")
	print("[Probe] 跳跃帧已保存，当前 y=%.1f 速度Y=%.1f" % [box.global_position.y, box.velocity.y])

	# 等落回
	for i in range(60):
		await physics_frame
	print("[Probe] 落回 y=%.1f 在地面=%s" % [box.global_position.y, box.is_on_floor()])

	quit()
