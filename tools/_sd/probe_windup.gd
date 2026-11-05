extends SceneTree

# 量一套连招里各阶段各占多久，确认蓄力真的是 0.2 秒

var _e: CharacterBody2D
var _box: CharacterBody2D

func _st() -> String:
	return str(_e.call("DebugPirate")).split("state=")[1].split(" ")[0]

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node); current_scene = node
	await process_frame; await physics_frame

	_e = node.get_node("pirate_apple1")
	_box = node.get_node("box")
	_e.set("DetectRange", 0.0)
	for c in node.get_children():
		if c is CharacterBody2D and c != _e and c != _box:
			c.set("Damage", 0); c.process_mode = Node.PROCESS_MODE_DISABLED

	print("Windup1Time=%.2f  Windup2Time=%.2f  Strike1Time=%.2f  Strike2Time=%.2f"
		% [_e.get("Windup1Time"), _e.get("Windup2Time"), _e.get("Strike1Time"), _e.get("Strike2Time")])

	_box.global_position = Vector2(_e.global_position.x - 90, _e.global_position.y - 60)
	for i in range(120):
		await physics_frame
		if _box.is_on_floor(): break
	_e.set("DetectRange", 460.0)

	var prev := ""
	var t0 := 0.0
	var segs: Array = []
	for i in range(700):
		await physics_frame
		var s := _st()
		if s != prev:
			if prev != "":
				segs.append("%s %.2fs" % [prev, t0 / 60.0])
			t0 = 0.0
			prev = s
		t0 += 1.0
		if segs.size() >= 10: break
	print("实际各阶段时长（按 60 帧/秒）:")
	for s in segs: print("  ", s)
	quit()
