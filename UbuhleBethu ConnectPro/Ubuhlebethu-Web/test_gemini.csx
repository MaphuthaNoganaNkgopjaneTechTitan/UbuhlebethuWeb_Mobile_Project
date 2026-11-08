using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Json;

var http = new HttpClient();
var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent?key=AQ.Ab8RN6LmqYvRG2HjruonV3Box6eIzsA5-Oy7PKOiwQn8QXzewg";
var body = new {
    systemInstruction = new { parts = new[] { new { text = "System context" } } },
    contents = new[] { new { role = "user", parts = new[] { new { text = "Hello" } } } },
    generationConfig = new { temperature = 0.3 }
};
var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
var resp = await http.SendAsync(msg);
Console.WriteLine(resp.StatusCode);
var str = await resp.Content.ReadAsStringAsync();
Console.WriteLine(str);
