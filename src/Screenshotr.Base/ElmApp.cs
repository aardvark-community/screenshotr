﻿using Microsoft.AspNetCore.Components.Web;
using System.Collections;
using System.Text.Json;

namespace Screenshotr
{
    public interface IElmApp<TModel, TMessageType>
    {
        void Dispatch(TMessageType messageType);
        void Dispatch<T>(TMessageType messageType, T messageArg);
    }

    public class ElmApp<TModel, TMessageType> : IElmApp<TModel, TMessageType>
    {
        public interface IMessage
        {
            public TMessageType MessageType { get; }
            public T GetArgument<T>();
        }

        public static class Message
        {
            public static IMessage Create(TMessageType msg) => new Message<Unit>(msg, Unit.Default);
            public static IMessage Create<T>(TMessageType msg, T arg) => new Message<T>(msg, arg);
        }

        private record Message<T>(TMessageType MessageType, T Arg) : IMessage
        {
            public T1 GetArgument<T1>() => (T1)(object)Arg!;

            public override string ToString()
            {
                switch (Arg)
                {
                    case Unit:
                    case bool:
                    case int:
                    case float:
                    case double:
                        return $"[{MessageType}; {Arg}]";
                    case string x:
                        return $"[{MessageType}; \"{x}\"]";
                    case MouseEventArgs x:
                        return $"[{MessageType}; {JsonSerializer.Serialize(x)}]";
                    case KeyboardEventArgs x:
                        return $"[{MessageType}; {JsonSerializer.Serialize(x)}]";
                    case IEnumerable xs:
                        {
                            var i = 0;
                            var ys = new List<object>();
                            var moreThanThree = false;
                            foreach (var x in xs)
                            {
                                if (i < 3) ys.Add(x);
                                if (i == 3) { moreThanThree = true; break; }
                                i++;
                            }
                            var dots = moreThanThree ? ", ..." : "";
                            var value = $"[{string.Join(", ", ys.Select(x => x.ToString()))}{dots}]";
                            return $"[{MessageType}; {value}; {typeof(T).Name}]";
                        }
                    default:
                        return $"[{MessageType}; {Arg}; {typeof(T).Name}]";
                }
            }
        }

        public class Unit
        {
            public static readonly Unit Default = new();
            public override string ToString() => "Unit";
        }

        private readonly Func<IElmApp<TModel, TMessageType>, IMessage, TModel, Task<TModel>> _update;

        public TModel Model { get; private set; }

        public ElmApp(TModel initialModel, Func<IElmApp<TModel, TMessageType>, IMessage, TModel, Task<TModel>> update)
        {
            Model = initialModel;
            _update = update;

            Run();
        }

        private readonly Queue<(DateTimeOffset timestamp, long id, IMessage msg)> _messages = new();
        private readonly SemaphoreSlim _messageIsAvailable = new(0);
        private long _msgId = 0;
        private void Run() => Task.Run(async () =>
        {
            var instanceId = Guid.NewGuid().ToString()[..8].ToUpperInvariant();
            var colorBg = (ConsoleColor)Random.Shared.Next(8);
            var colorFg = (ConsoleColor)(~(int)colorBg & 0b1111);
            //while (colorBg == colorFg) colorFg = (ConsoleColor)Random.Shared.Next(16);

            while (true)
            {
                await _messageIsAvailable.WaitAsync();

                DateTimeOffset t0; long msgId; IMessage msg;
                lock (_messages) (t0, msgId, msg) = _messages.Dequeue();

                var t1 = DateTimeOffset.Now;
                try
                {
                    var newModel = await _update(this, msg, Model);
                    if (!ReferenceEquals(newModel, Model))
                    {
                        Model = newModel;
                        NotifyStateChanged();
                    }
                }
                catch (Exception e)
                {
                    Console.Write($"[{t0.Year:0000}-{t0.Month:00}-{t0.Day:00} {t0.Hour:00}:{t0.Minute:00}:{t0.Second:00}.{t0.Millisecond:000}][");
                    Console.BackgroundColor = colorBg;
                    Console.ForegroundColor = colorFg;
                    Console.Write(instanceId);
                    Console.ResetColor();
                    Console.WriteLine($"/{msgId,6}][ERR] {e}");
                }
                finally
                {
                    var t2 = DateTimeOffset.Now;
                    Console.Write($"[{t0.Year:0000}-{t0.Month:00}-{t0.Day:00} {t0.Hour:00}:{t0.Minute:00}:{t0.Second:00}.{t0.Millisecond:000}][");
                    Console.BackgroundColor = colorBg;
                    Console.ForegroundColor = colorFg;

                    Console.Write(instanceId);
                    Console.ResetColor();
                    Console.WriteLine($"/{msgId,6}][MSG] {(int)(t1 - t0).TotalMilliseconds,4} ms | {(int)(t2 - t1).TotalMilliseconds,4} ms | {msg}");
                }
            }
        });

        private void Dispatch(IMessage msg)
        {
            if (msg == null)
            {
                Console.WriteLine($"[ERR] empty message");
                return;
            }

            var t0 = DateTimeOffset.Now;
            var id = Interlocked.Increment(ref _msgId);

            lock (_messages)
            {
                _messages.Enqueue((t0, id, msg));
                _messageIsAvailable.Release();
            }
        }

        public void Dispatch<T>(TMessageType messageType, T messageArg) => Dispatch(Message.Create(messageType, messageArg));

        public void Dispatch(TMessageType messageType) => Dispatch(Message.Create(messageType));

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
