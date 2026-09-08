# Export de modelos a formatos

#ONNX
import torch
import onnx
import onnxruntime

def ONNX_export(model, dummy_input_shape, output_name):
    model.eval()

    # Dummy input con la forma de lo que recibe el modelo
    dummy_input = torch.randn(*dummy_input_shape) # Si se cambia estructura cambiar 
    print(dummy_input.shape)

    onnx_program = torch.onnx.export(
        model,
        dummy_input,
        output_name + ".onnx",
        export_params=True,
        opset_version=15,
        input_names=['input'],
        output_names=['output'],
    )

    # Verificar que el modelo ONNX es válido
    onnx_model = onnx.load(output_name + ".onnx")
    onnx.checker.check_model(onnx_model)

    # Test de inferencia con ONNXRuntime
    ort_session = onnxruntime.InferenceSession(output_name + ".onnx")
    ort_out = ort_session.run(None, {'input': dummy_input.numpy()})
    print(f"ONNX output shape: {ort_out[0].shape}")
    print("ONNX export llevado a cabo con éxito.")

    