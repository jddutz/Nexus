using System.Numerics;
using Nexus.Assets.Typography.Geometry;

namespace Nexus.Assets.Typography.FontReader.OpenType;

/// <summary>Interprets bounded Type 2 programs and converts cubic paths to quadratic geometry.</summary>
internal sealed class Type2CharString(ReadOnlyMemory<byte>[] globalSubrs, ReadOnlyMemory<byte>[] localSubrs)
{
    private readonly List<double> _stack = [];
    private readonly List<Contour> _contours = [];
    private readonly List<Edge> _edges = [];
    private readonly double[] _transient = new double[32];
    private Vector2 _point;
    private Vector2? _start;
    private bool _widthRead;
    private bool _ended;
    private int _stems;
    private int _operations;
    private int _segments;

    internal IReadOnlyList<Contour> Decode(ReadOnlyMemory<byte> program)
    {
        Execute(program, 0);
        if (!_ended) throw Invalid("Missing endchar.");
        return _contours.ToArray();
    }

    private void Execute(ReadOnlyMemory<byte> program, int depth)
    {
        if (depth > 10) throw Invalid("Subroutine nesting exceeds ten calls.");
        var reader = new CffDataReader(program);
        while (reader.Position < reader.Length && !_ended)
        {
            if (++_operations > 100000) throw Invalid("Charstring instruction limit exceeded.");
            var op = reader.Byte();
            if (op >= 32 || op == 28)
            {
                Push(reader.Number(op, false));
                continue;
            }
            if (op is 10 or 29)
            {
                var subrs = op == 10 ? localSubrs : globalSubrs;
                var bias = subrs.Length < 1240 ? 107 : subrs.Length < 33900 ? 1131 : 32768;
                var operand = Pop();
                if (operand != Math.Truncate(operand) || operand + bias < 0 || operand + bias >= subrs.Length)
                    throw Invalid("Invalid subroutine index.");
                Execute(subrs[(int)operand + bias], depth + 1);
                continue;
            }
            if (op == 11)
            {
                if (depth == 0) throw Invalid("Return outside a subroutine.");
                return;
            }
            if (op == 12)
            {
                Escape(reader.Byte());
                continue;
            }
            if (op is 1 or 3 or 18 or 23 or 19 or 20)
            {
                Width(_stack.Count % 2 != 0);
                if (_stack.Count % 2 != 0) throw Invalid("Odd stem operand count.");
                _stems += _stack.Count / 2;
                if (_stems > 96) throw Invalid("Stem count exceeds 96.");
                _stack.Clear();
                if (op is 19 or 20)
                    for (var i = 0; i < (_stems + 7) / 8; i++) _ = reader.Byte();
                continue;
            }
            if (op is 4 or 21 or 22)
            {
                var count = op == 21 ? 2 : 1;
                Width(_stack.Count == count + 1);
                Count(count);
                Close();
                _point += op == 21 ? V(_stack[0], _stack[1]) : op == 22 ? V(_stack[0], 0) : V(0, _stack[0]);
                _start = _point;
            }
            else if (op == 14)
            {
                Width(_stack.Count is 1 or 5);
                if (_stack.Count != 0) throw Invalid("Deprecated endchar composite outlines are not supported.");
                Close();
                _ended = true;
            }
            else
            {
                if (_start is null) throw Invalid("Path operator before moveto.");
                Draw(op);
            }
            _stack.Clear();
        }
        if (!_ended) throw Invalid(depth == 0 ? "Missing endchar." : "Missing subroutine return.");
    }

    private void Draw(int op)
    {
        var a = _stack;
        var n = a.Count;
        switch (op)
        {
            case 5:
                Multiple(2);
                for (var i = 0; i < n; i += 2) Line(a[i], a[i + 1]);
                break;
            case 6:
            case 7:
                if (n == 0) throw Invalid("Empty line operands.");
                var horizontal = op == 6;
                foreach (var d in a) { Line(horizontal ? d : 0, horizontal ? 0 : d); horizontal = !horizontal; }
                break;
            case 8:
                Multiple(6);
                for (var i = 0; i < n; i += 6) Curve(a[i], a[i + 1], a[i + 2], a[i + 3], a[i + 4], a[i + 5]);
                break;
            case 24:
                if (n < 8 || (n - 2) % 6 != 0) throw Invalid("Invalid rcurveline operands.");
                for (var i = 0; i < n - 2; i += 6) Curve(a[i], a[i + 1], a[i + 2], a[i + 3], a[i + 4], a[i + 5]);
                Line(a[n - 2], a[n - 1]);
                break;
            case 25:
                if (n < 8 || (n - 6) % 2 != 0) throw Invalid("Invalid rlinecurve operands.");
                for (var i = 0; i < n - 6; i += 2) Line(a[i], a[i + 1]);
                Curve(a[n - 6], a[n - 5], a[n - 4], a[n - 3], a[n - 2], a[n - 1]);
                break;
            case 26:
            case 27:
                if (n < 4 || n % 4 > 1) throw Invalid("Invalid vv/hhcurveto operands.");
                var extra = n % 4 == 1 ? a[0] : 0;
                for (var i = n % 4; i < n; i += 4)
                {
                    if (op == 26) Curve(extra, a[i], a[i + 1], a[i + 2], 0, a[i + 3]);
                    else Curve(a[i], extra, a[i + 1], a[i + 2], a[i + 3], 0);
                    extra = 0;
                }
                break;
            case 30:
            case 31:
                if (n < 4 || n % 4 > 1) throw Invalid("Invalid vh/hvcurveto operands.");
                var h = op == 31;
                for (var i = 0; i + 3 < n; i += 4)
                {
                    var last = n - i == 5 ? a[i + 4] : 0;
                    if (h) Curve(a[i], 0, a[i + 1], a[i + 2], last, a[i + 3]);
                    else Curve(0, a[i], a[i + 1], a[i + 2], a[i + 3], last);
                    h = !h;
                }
                break;
            default: throw Invalid($"Unsupported Type 2 operator {op}.");
        }
    }

    private void Escape(int op)
    {
        if (op is >= 34 and <= 37)
        {
            if (_start is null) throw Invalid("Flex before moveto.");
            var a = _stack.ToArray();
            switch (op)
            {
                case 34:
                    Count(7);
                    Curve(a[0], 0, a[1], a[2], a[3], 0);
                    Curve(a[4], 0, a[5], -a[2], a[6], 0);
                    break;
                case 35:
                    Count(13);
                    Curve(a[0], a[1], a[2], a[3], a[4], a[5]);
                    Curve(a[6], a[7], a[8], a[9], a[10], a[11]);
                    break;
                case 36:
                    Count(9);
                    Curve(a[0], a[1], a[2], a[3], a[4], 0);
                    Curve(a[5], 0, a[6], a[7], a[8], -(a[1] + a[3] + a[7]));
                    break;
                case 37:
                    Count(11);
                    var dx = a[0] + a[2] + a[4] + a[6] + a[8];
                    var dy = a[1] + a[3] + a[5] + a[7] + a[9];
                    Curve(a[0], a[1], a[2], a[3], a[4], a[5]);
                    Curve(a[6], a[7], a[8], a[9], Math.Abs(dx) > Math.Abs(dy) ? a[10] : -dx, Math.Abs(dx) > Math.Abs(dy) ? -dy : a[10]);
                    break;
            }
            _stack.Clear();
            return;
        }
        double x, y;
        switch (op)
        {
            case 3: y = Pop(); x = Pop(); Push(x != 0 && y != 0 ? 1 : 0); break;
            case 4: y = Pop(); x = Pop(); Push(x != 0 || y != 0 ? 1 : 0); break;
            case 5: Push(Pop() == 0 ? 1 : 0); break;
            case 9: Push(Math.Abs(Pop())); break;
            case 10: y = Pop(); x = Pop(); Push(x + y); break;
            case 11: y = Pop(); x = Pop(); Push(x - y); break;
            case 12: y = Pop(); x = Pop(); Push(x / y); break;
            case 14: Push(-Pop()); break;
            case 15: y = Pop(); x = Pop(); Push(x == y ? 1 : 0); break;
            case 18: _ = Pop(); break;
            case 20: var put = TransientIndex(Pop()); _transient[put] = Pop(); break;
            case 21: Push(_transient[TransientIndex(Pop())]); break;
            case 22: var v2 = Pop(); var v1 = Pop(); y = Pop(); x = Pop(); Push(v1 <= v2 ? x : y); break;
            case 23: Push(.5); break; // deterministic value in the specified (0, 1] range
            case 24: y = Pop(); x = Pop(); Push(x * y); break;
            case 26: Push(Math.Sqrt(Pop())); break;
            case 27: x = Pop(); Push(x); Push(x); break;
            case 28: y = Pop(); x = Pop(); Push(y); Push(x); break;
            case 29:
                var index = Pop();
                if (index != Math.Truncate(index) || _stack.Count == 0) throw Invalid("Invalid index operand.");
                Push(_stack[(int)Math.Clamp(_stack.Count - 1 - index, 0, _stack.Count - 1)]);
                break;
            case 30:
                var shift = Pop(); var count = Pop();
                if (count < 0 || count > _stack.Count || count != Math.Truncate(count) || shift != Math.Truncate(shift)) throw Invalid("Invalid roll operands.");
                if (count > 0)
                {
                    var values = _stack.GetRange(_stack.Count - (int)count, (int)count);
                    var rotation = (int)((shift % count + count) % count);
                    for (var i = 0; i < values.Count; i++) _stack[_stack.Count - values.Count + (i + rotation) % values.Count] = values[i];
                }
                break;
            default: throw Invalid($"Unsupported escaped Type 2 operator {op}.");
        }
    }

    private void Width(bool present)
    {
        if (_widthRead) return;
        if (present) _stack.RemoveAt(0); // hmtx is authoritative for OpenType advances.
        _widthRead = true;
    }
    private void Push(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > 1e9) throw Invalid("Invalid numeric result.");
        if (_stack.Count >= 48) throw Invalid("Operand stack exceeds 48 entries.");
        _stack.Add(value);
    }
    private double Pop()
    {
        if (_stack.Count == 0) throw Invalid("Operand stack underflow.");
        var value = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); return value;
    }
    private void Count(int count)
    {
        if (_stack.Count != count) throw Invalid("Invalid operand count.");
    }
    private void Multiple(int count)
    {
        if (_stack.Count == 0 || _stack.Count % count != 0) throw Invalid("Invalid operand count.");
    }
    private static int TransientIndex(double value)
    {
        if (value < 0 || value >= 32 || value != Math.Truncate(value)) throw Invalid("Invalid transient array index.");
        return (int)value;
    }
    private static Vector2 V(double x, double y) => new((float)x, (float)y);
    private void Line(double dx, double dy)
    {
        var end = _point + V(dx, dy);
        AddEdge(new LineSegment(_point, end)); _point = end;
    }
    private void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        var c1 = _point + V(dx1, dy1); var c2 = c1 + V(dx2, dy2); var end = c2 + V(dx3, dy3);
        Quadratics(_point, c1, c2, end, 0); _point = end;
    }
    private void Quadratics(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int depth)
    {
        // Degree reduction: the two implied quadratic controls converge under subdivision.
        var q1 = (3 * p1 - p0) / 2; var q2 = (3 * p2 - p3) / 2;
        if (Vector2.Distance(q1, q2) <= .04f)
        {
            AddEdge(new QuadraticSegment(p0, (q1 + q2) / 2, p3));
            return;
        }
        if (depth >= 16) throw Invalid("Cubic subdivision limit exceeded.");
        var a = (p0 + p1) / 2; var b = (p1 + p2) / 2; var c = (p2 + p3) / 2;
        var d = (a + b) / 2; var e = (b + c) / 2; var mid = (d + e) / 2;
        Quadratics(p0, a, d, mid, depth + 1); Quadratics(mid, e, c, p3, depth + 1);
    }
    private void Close()
    {
        if (_start is { } start && _edges.Count > 0)
        {
            if (_point != start) AddEdge(new LineSegment(_point, start));
            _contours.Add(new Contour(_edges.ToArray()));
        }
        _edges.Clear(); _start = null;
    }
    private static InvalidDataException Invalid(string message) => new($"Invalid or unsupported CFF Type 2 charstring: {message}");

    private void AddEdge(Edge edge)
    {
        if (++_segments > 100000) throw Invalid("Glyph geometry limit exceeded.");
        _edges.Add(edge);
    }
}
