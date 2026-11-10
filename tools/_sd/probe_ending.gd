extends SceneTree

# 结局流程验证：站到奶蛙旁边狂按 E → 黑屏 → 字幕 → 标记通关
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var gs: Node = root.get_node_or_null("GameState")
	if gs == null:
		print("[End] 没找到 GameState autoload")
		quit()
		return

	var box: Node2D = scn.get_node("box")
	var naiwa: Node2D = scn.get_node_or_null("naiwa")
	var ending: Node = scn.get_node_or_null("EndingSequence")
	if naiwa == null or ending == null:
		print("[End] 节点缺失 naiwa=%s ending=%s" % [str(naiwa), str(ending)])
		quit()
		return

	# 把演出调快，不然要等 20 多秒
	ending.FadeOutTime = 0.3
	ending.BlackHoldTime = 0.2
	ending.ScrollSpeed = 120.0
	ending.EndHoldTime = 0.2

	box.global_position = naiwa.global_position + Vector2(-60, 0)
	await create_timer(0.6).timeout
	print("[End] 玩家=%s 奶蛙=%s" % [str(box.global_position), str(naiwa.global_position)])

	var presses := 0
	for i in range(60):
		# 必须走 parse_input_event：action_press 不会产生 JustPressed 的那一帧
		var down := InputEventAction.new()
		down.action = "interact"
		down.pressed = true
		Input.parse_input_event(down)
		await process_frame
		await process_frame
		var up := InputEventAction.new()
		up.action = "interact"
		up.pressed = false
		Input.parse_input_event(up)
		await create_timer(0.12).timeout
		presses += 1
		# 对话说完（结局已启动）就停手，别再按 —— 黑屏之后按 E 是"跳过字幕"
		if scn.get_node_or_null("EndingLayer") != null or scn.find_child("EndingLayer", true, false) != null:
			print("[End] 第 %d 次按键后结局启动" % presses)
			break

	var am: Node = root.get_node_or_null("AudioManager")
	print("[End] 结局启动后 BGM=%s" % (str(am.CurrentBgm) if am else "无"))
	await create_timer(1.2).timeout
	print("[End] 黑屏后 BGM=%s" % (str(am.CurrentBgm) if am else "无"))
	print("[End] Cleared=%s DialogueActive=%s" % [
		str(gs.Cleared), str(gs.DialogueActive)])

	# 抓两张字幕画面
	for i in range(12):
		await create_timer(0.25).timeout
		if i in [4, 10]:
			var im := root.get_viewport().get_texture().get_image()
			if im != null: im.save_png("res://docs/screenshots/ending_%02d.png" % i)
			print("[End] 截图 %d" % i)

	# 等它切场景
	await create_timer(2.0).timeout
	print("[End] 主菜单 BGM=%s  当前场景=%s" % [
		str(am.CurrentBgm) if am else "无", str(root.get_tree().current_scene)])
	var img := root.get_viewport().get_texture().get_image()
	if img != null: img.save_png("res://docs/screenshots/ending_check.png")
	quit()
