"""Author engine-native Blueprint graphs; all policy values come from the shared contract.

Blueprint bytecode cannot execute the managed Domain state types. This adapter maps their
operations to native math nodes; acceptance requires the shared transition vectors in Unreal.
It never inserts calls to the editor helper into customer runtime graphs.
"""


class Graph:
    """Small checked graph-authoring adapter; reject missing pins and failed connections early."""

    def __init__(self, u, blueprint, name, override=False, event=None):
        u.log("PB preview graph: " + name)
        self.u = u
        self.blueprint = blueprint
        if event is not None:
            self.editor = u.BlueprintGraphEditor.get_graph_editor_by_name(blueprint, "EventGraph")
            self.tail = event.find_then_pin()
        elif override:
            self.editor = u.BlueprintGraphEditor.get_graph_editor(
                u.BlueprintEditorLibrary.add_function_override(blueprint, name)
            )
            self.tail = self.editor.find_graph_entry_pin()
            self.tail.break_pin_links()
        else:
            self.editor = u.BlueprintGraphEditor.create_and_edit_function_graph(blueprint, name)
            self.tail = self.editor.find_graph_entry_pin()

    def parameter(self, name):
        return self.editor.find_graph_entry_pin().get_owning_node().find_output_pin(name)

    def result(self, value):
        node = self.editor.add_return_node()
        self.connect(self.tail, node.find_execute_pin())
        self.connect(value, node.find_input_pin("ReturnValue"))

    def branch(self, condition):
        node = self.editor.add_branch_node()
        self.connect(self.tail, node.find_execute_pin())
        self.connect(condition, node.find_input_pin("Condition"))
        self.tail = node.find_output_pin("then")
        return node.find_output_pin("else")

    def pure(g, path, **inputs):
        return g.node(path, **inputs).find_output_pin("ReturnValue")

    def local(g, name, **inputs):
        return g.call(name, **inputs)

    def self_pin(self):
        return self.get("SelfWidget")

    def input(self, name, kind="real"):
        self.editor.add_graph_input_parameter(
            name, self.u.BlueprintEditorLibrary.get_basic_type_by_name(kind)
        )
        # Refresh the skeleton signature before UE's operator-promotion machinery inspects it.
        if not self.u.BlueprintEditorLibrary.compile_blueprint(self.blueprint):
            raise RuntimeError("Preview function signature compilation failed.")
        return self.parameter(name)

    def connect(self, value, pin):
        if not pin.is_valid():
            raise RuntimeError("Missing native Blueprint pin.")
        if isinstance(value, self.u.BlueprintGraphPin):
            if not value.try_create_connection(pin):
                raise RuntimeError("Incompatible native Blueprint pins.")
        elif not pin.set_pin_value(str(value).lower() if isinstance(value, bool) else str(value)):
            raise RuntimeError("Native Blueprint default assignment failed.")

    def node(g, path, **inputs):
        node = g.u.PackageBuilderPreviewEditorLibrary.add_native_call(g.editor.get_graph(), path)
        if node is None:
            raise RuntimeError("Native Blueprint function was not found: " + path)
        for name, value in inputs.items():
            pin = node.find_input_pin(name)
            if not pin.is_valid():
                raise RuntimeError(path + " missing pin " + name)
            g.connect(value, pin)
        return node

    def call(g, path, **inputs):
        node = g.node(path, **inputs)
        g.connect(g.tail, node.find_execute_pin())
        g.tail = node.find_then_pin()
        return node

    def math(self, name, **inputs):
        return self.node("/Script/Engine.KismetMathLibrary." + name, **inputs).find_output_pin(
            "ReturnValue"
        )

    def get(self, name):
        return self.editor.add_get_member_variable_node(name).find_output_pin(name)

    def set(self, name, value):
        node = self.editor.add_set_member_variable_node(name)
        self.connect(value, node.find_input_pin(name))
        self.connect(self.tail, node.find_execute_pin())
        self.tail = node.find_then_pin()

    def guard(self, value):
        """A false input ends this function without mutating its state."""
        node = self.editor.add_branch_node()
        self.connect(self.tail, node.find_execute_pin())
        self.connect(value, node.find_input_pin("Condition"))
        self.tail = node.find_output_pin("then")

    def finite(self, value):
        return self.math(
            "LessEqual_DoubleDouble", A=self.math("Abs", A=value), B=1.7976931348623157e308
        )

    def sum(self, a, b):
        return self.math("Add_DoubleDouble", A=a, B=b)

    def product(self, a, b):
        return self.math("Multiply_DoubleDouble", A=a, B=b)

    def clamp(self, value, minimum, maximum):
        return self.math("FClamp", Value=value, Min=minimum, Max=maximum)

    def yaw(self, value):
        # Two remainders preserve the Domain's [-180, 180) convention for negative inputs.
        first = self.math("Percent_FloatFloat", A=self.sum(value, 180), B=360)
        positive = self.math("Percent_FloatFloat", A=self.sum(first, 360), B=360)
        return self.math("Subtract_DoubleDouble", A=positive, B=180)


def state_functions(u, blueprint, contract, apply=False):
    """Compile version-one orbit, zoom and lighting transitions without engine-side defaults."""
    navigation, light = contract["navigation"], contract["lightControls"]
    for name, yaw, pitch, policy in (
        ("Orbit", "Yaw", "Pitch", navigation),
        ("AdjustLight", "LightYaw", "LightPitch", light),
    ):
        graph = Graph(u, blueprint, name)
        dx, dy = graph.input("DeltaYaw"), graph.input("DeltaPitch")
        graph.guard(graph.math("BooleanAND", A=graph.finite(dx), B=graph.finite(dy)))
        graph.set(yaw, graph.yaw(graph.sum(graph.get(yaw), dx)))
        graph.set(
            pitch,
            graph.clamp(
                graph.sum(graph.get(pitch), dy),
                policy["minimumPitchDegrees"],
                policy["maximumPitchDegrees"],
            ),
        )
        if apply:
            graph.local("ApplyCamera" if name == "Orbit" else "ApplyLight")
    graph = Graph(u, blueprint, "Zoom")
    steps, fraction = graph.input("Steps"), graph.input("Fraction")
    valid = graph.math("BooleanAND", A=graph.finite(steps), B=graph.finite(fraction))
    valid = graph.math("BooleanAND", A=valid, B=graph.math("Greater_DoubleDouble", A=fraction, B=0))
    valid = graph.math("BooleanAND", A=valid, B=graph.math("Less_DoubleDouble", A=fraction, B=1))
    graph.guard(valid)
    factor = graph.math(
        "MultiplyMultiply_FloatFloat",
        Base=graph.math("Subtract_DoubleDouble", A=1, B=fraction),
        Exp=steps,
    )
    graph.set(
        "Distance",
        graph.clamp(
            graph.product(graph.get("Distance"), factor),
            navigation["minimumDistanceMultiplier"],
            navigation["maximumDistanceMultiplier"],
        ),
    )
    if apply:
        graph.local("ApplyCamera")
