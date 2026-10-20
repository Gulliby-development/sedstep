namespace Sedstep;

internal abstract record Node;

internal sealed record SequenceNode(IReadOnlyList<Node> Children) : Node;

internal sealed record LiteralNode(int Value) : Node;

internal sealed record AnyNode : Node;

internal sealed record AnchorNode(bool IsStart) : Node;

internal sealed record GroupNode(int Number, Node Child) : Node;

internal sealed record RepeatNode(Node Child, int Minimum, int? Maximum) : Node;

internal sealed record BracketNode(bool Negated, IReadOnlyList<BracketItem> Items) : Node;

internal abstract record BracketItem;

internal sealed record CharacterItem(int Value) : BracketItem;

internal sealed record RangeItem(int First, int Last) : BracketItem;

internal sealed record ClassItem(CharacterClass Class) : BracketItem;

internal enum CharacterClass
{
    Alpha,
    Digit,
    Alnum,
    Upper,
    Lower,
    Space,
}

internal sealed record ParsedPattern(Node Root, int Groups);

