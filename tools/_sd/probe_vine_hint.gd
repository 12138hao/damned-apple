extends SceneTree

# ① 站在藤底时 W 提示要出现 ② 松开 W 后左右键要能走（不能被攀爬锁住）
# ③ 老绳索（underground.tscn）不能被基类改动搞坏

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame
	var box: CharacterBody2D = node.get_node("box")
	var v1: Node = node.get_node("Vine1")
	var marker: Sprite2D = v1.get_node("WMarker")

	box.global_position = Vector2(620, 340)
	for i in range(10):
		await physics_frame
	print("站在藤底：W 提示可见=", marker.visible, "  位置=", marker.position, "  ", box.call("DebugClimb"))

	Input.action_press("look_up")
	for i in range(20):
		await physics_frame
	Input.action_release("look_up")
	print("爬了20帧后：提示可见=", marker.visible, "（爬上去了应该隐藏）")

	# 回到地面，松手后能不能走
	box.global_position = Vector2(620, 340)
	for i in range(10):
		await physics_frame
	Input.action_press("look_up")
	for i in range(4):
		await physics_frame
	Input.action_release("look_up")
	for i in range(4):
		await physics_frame
	var x0: float = box.global_position.x
	Input.action_press("move_right")
	for i in range(20):
		await physics_frame
	Input.action_release("move_right")
	print("松手后按右 20 帧：x %.1f → %.1f（应该明显变大，说明没被攀爬锁住）" % [x0, box.global_position.x])
	print("  ", box.call("DebugClimb"))
	node.queue_free()

	# ── 老绳索回归 ──
	var under: PackedScene = load("res://underground.tscn")
	var u := under.instantiate()
	root.add_child(u)
	await process_frame
	var rope: Node = u.get_node_or_null("Rope")
	if rope == null:
		print("!! underground 里找不到 Rope"); quit(); return
	print("绳索: TopY=", rope.get("TopY"), " BottomY=", rope.get("BottomY"),
		" 上速=", rope.call("get_up_speed") if rope.has_method("get_up_speed") else "（C#属性读不到，跳过）")
	var ub: CharacterBody2D = u.get_node_or_null("box")
	if ub != null:
		ub.global_position = Vector2(24500, 470)
		for i in range(12):
			await physics_frame
		print("绳索旁：", ub.call("DebugClimb"))
		Input.action_press("look_up")
		var y0: float = ub.global_position.y
		for i in range(30):
			await physics_frame
		Input.action_release("look_up")
		print("按住W 30帧：y %.1f → %.1f（往上爬了 %.1f 像素）" % [y0, ub.global_position.y, y0 - ub.global_position.y])
	quit()
