// See https://aka.ms/new-console-template for more information

using Reef.Core;

const string mediumSource = """
                            pub fn DoSomething(a: int): result<int, string> {
                                var b = 2;
                                
                                if (a > b) {
                                    return ok(a);
                                }
                                else if (a == b) {
                                    return ok(b);
                                }
                                
                                return error("something wrong");
                            }

                            pub fn SomethingElse(a: int): result<int, string> {
                                b = DoSomething(a)?;
                                
                                return b;
                            }

                            Println(DoSomething(5));
                            Println(DoSomething(1));
                            Println(SomethingElse(1));

                            """;

const string largeSource = $"""
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            {mediumSource}
                            """;

var tokenizer = Tokenizer.Tokenize(largeSource);

var a = tokenizer.ToArray();