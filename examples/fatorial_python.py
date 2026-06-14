try:
    entrada = input("Digite um número: ")
    numero = int(entrada)

    if numero < 0:
        print("Não existe fatorial de número negativo.")
    else:
        fatorial = 1
        for i in range(1, numero + 1):
            fatorial *= i

        print(f"\nCálculo do fatorial de {numero}:")

        for i in range(numero, 0, -1):
            print(i, end=" x " if i > 1 else "")

        print(f" = {fatorial}")

except ValueError:
    print("Entrada inválida. Por favor, digite um número inteiro.")

print("\nPrograma finalizado.")
