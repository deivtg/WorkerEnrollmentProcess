using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using ServiceReference;
using WorkerEnrollmentProcess.Models;

public class Program
{
    private static AppLog AppLog = new AppLog();
    private static IModel channel = null;
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.File("WorkerEnrollmentManagement.out", Serilog.Events.LogEventLevel.Debug, "{Message:lj}{NewLine}", encoding: Encoding.UTF8).CreateLogger();

        AppLog.ResponseTime = Convert.ToInt16(DateTime.Now.ToString("fff"));
        AppLog.DateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
        AppLog.ResponseCode = 0;

        IConnection conexion = null;
        var connetionFactory = new ConnectionFactory();
        connetionFactory.Port = 5672;
        connetionFactory.UserName = "guest";
        connetionFactory.Password = "guest";
        conexion = connetionFactory.CreateConnection();
        channel = conexion.CreateModel();
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += ConsumerReceived;
        var consumerTag = channel.BasicConsume("kalum.queue.enrollment", true, consumer);
        Console.WriteLine("Presione enter para finalizar el proceso");
        Console.ReadLine();
    }

    private static async void ConsumerReceived(object? sender, BasicDeliverEventArgs e){
        string message = Encoding.UTF8.GetString(e.Body.ToArray());
        EnrollmentRequest request = JsonSerializer.Deserialize<EnrollmentRequest>(message);
        ImprimirLog(0, JsonSerializer.Serialize(request), "Debug");
        EnrollmentResponse enrollmentResponse = await ClientWebService(request);
        if(enrollmentResponse != null && enrollmentResponse.Codigo != 201){
            channel.BasicPublish("", "kalum.queue.failed-enrollment", null, Encoding.UTF8.GetBytes(message));
        }else{
            AppLog.Message = "Se proceso la inscripcion de forma exitosa";
            ImprimirLog(201, JsonSerializer.Serialize(AppLog), "Information");
        }
    }

    public static async Task<EnrollmentResponse> ClientWebService(EnrollmentRequest request){
        EnrollmentResponse enrollmentResponse = null;
        try{
            var client = new EnrollmentServiceClient(EnrollmentServiceClient.EndpointConfiguration.BasicHttpBinding_IEnrollmentService_soap, "http://localhost:5144/EnrollmentService.asmx");
        var response = await client.EnrollmentProcessAsync(request);
        enrollmentResponse = new EnrollmentResponse(){Codigo = response.Body.EnrollmentProcessResult.Codigo, Respuesta = response.Body.EnrollmentProcessResult.Respuesta, Carne = response.Body.EnrollmentProcessResult.Carne};
        if(enrollmentResponse.Codigo == 503){
            ImprimirLog(503, enrollmentResponse.Respuesta, "Error");
        }else{
            ImprimirLog(201, $"Se finalizo el proceso de inscripcion con el carne {enrollmentResponse.Carne}", "Information");
        }
        }catch(Exception e){
            enrollmentResponse = new EnrollmentResponse() { Codigo = 500, Respuesta = e.Message, Carne = "0"};
            ImprimirLog(500, $"Error en el sistema: {e.Message}", "Error");
        }
        return enrollmentResponse;
    }

    private static void ImprimirLog(int responseCode, string message, string typeLog){
        AppLog.ResponseCode = responseCode;
        AppLog.Message = message;
        AppLog.ResponseTime = Convert.ToInt16(DateTime.Now.ToString("fff")) - AppLog.ResponseTime;
        if(typeLog.Equals("Error")){
            AppLog.Level = 40;
            Log.Error(JsonSerializer.Serialize(AppLog));
        }else if(typeLog.Equals("Information")){
            AppLog.Level = 20;
            Log.Information(JsonSerializer.Serialize(AppLog));
        }else if(typeLog.Equals("Debug")){
            AppLog.Level = 10;
            Log.Debug(JsonSerializer.Serialize(AppLog));
        }
    }
}
