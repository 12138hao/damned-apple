extends SceneTree

# 端到端验证老苹果：首次对话 → 再次对话弹四选一 → 滚轮切换 → 点击选第 3 项

var _box: CharacterBody2D
var _npc: Node2D
var _gs: Node


func _dlg() -> bool:
	return bool(_gs.get("DialogueActive")) if _gs != null else false


func _press_e(n := 2) -> void:
	Input.action_press("interact")
	for i in range(n):
		await process_frame
	Input.action_release("interact")
	for i in range(3):
		await process_frame


func _bubble() -> String:
	var lb = _npc.get_node_or_null("DialogueUI/Text")
	if lb == null or not lb.visible:
		return "<没显示>"
	return lb.text


func _initialize() -> void:
	var node: Node = load("res://jungleinside.tscn").instantiate()
	root.add_child(node)
	current_scene = node
	await process_frame
	await physics_frame

	_gs = root.get_node_or_null("GameState")
	_box = node.get_node("box")
	_npc = node.get_node("OldApple")
	var spr: Sprite2D = _npc.get_node("Sprite2D")
	print("老苹果 pos=%s  贴图=%s  %s" % [_npc.global_position, spr.texture.resource_path, spr.texture.get_size()])

	_box.global_position = Vector2(_npc.global_position.x - 90, _npc.global_position.y - 60)
	for i in range(120):
		await physics_frame
		if _box.is_on_floor():
			break
	for i in range(10):
		await process_frame
	print("玩家落位 (%.0f, %.0f)  距离=%.0f" % [_box.global_position.x, _box.global_position.y,
		_npc.global_position.distance_to(_box.global_position)])

	print("")
	print("【第一次按 E】")
	await _press_e()
	print("  气泡: ", _bubble())
	print("  菜单可见: ", _npc.get_node("ChoiceMenu").visible)
	var n := 0
	while n < 24 and _dlg():
		await _press_e()
		n += 1
	print("  再按 %d 次后结束，对话中=%s" % [n, _dlg()])

	print("")
	print("【第二次按 E → 四选一】")
	await _press_e()
	var m: Node = _npc.get_node("ChoiceMenu")
	print("  菜单可见=%s  当前项=%s" % [m.visible, m.get("Current")])
	for i in range(4):
		var lb = m.get_node_or_null("Row%d" % (i + 1))
		print("    Row%d  visible=%s  text=%s" % [i + 1, lb.visible, lb.text])

	for k in range(2):
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_WHEEL_DOWN
		ev.pressed = true
		Input.parse_input_event(ev)
		for i in range(2):
			await process_frame
	print("  滚轮下滚 x2 → 当前项=%s" % m.get("Current"))

	var gt: Transform2D = m.get_global_transform_with_canvas()
	var vpos: Vector2 = gt * Vector2(0, 16.0)
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	click.position = vpos
	Input.parse_input_event(click)
	for i in range(4):
		await process_frame
	print("  点击第3行(视口 %s) → 菜单可见=%s  气泡=%s" % [str(vpos.round()), m.visible, _bubble()])

	n = 0
	while n < 24 and _dlg():
		await _press_e()
		n += 1
	print("  收尾：对话中=%s" % _dlg())
	quit()
