#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>

typedef struct {
	size_t length;
	const char *start;
} string;

void print_string(string str)
{
	fputs(str.start, stdout);
}

#define DEFINE_PRINT_INT(func_name, int_type)                            \
void func_name(int_type num)                                             \
{                                                                        \
    /* Handle negative numbers safely */                                 \
    if (num < 0)                                                         \
    {                                                                    \
        putchar('-');                                                    \
                                                                         \
        /* Convert to positive without overflow */                       \
        int_type n = -(num + 1);                                         \
        n += 1;                                                          \
        num = n;                                                         \
    }                                                                    \
                                                                         \
    if (num > 9)                                                         \
    {                                                                    \
        int_type a = num / 10;                                           \
        num -= a * 10;                                                   \
        func_name(a);                                                    \
    }                                                                    \
                                                                         \
    putchar('0' + (int)num);                                             \
}

DEFINE_PRINT_INT(print_i8, int8_t)
DEFINE_PRINT_INT(print_i16, int16_t)
DEFINE_PRINT_INT(print_i32, int32_t)
DEFINE_PRINT_INT(print_i64, int64_t)

DEFINE_PRINT_INT(print_u8, uint8_t)
DEFINE_PRINT_INT(print_u16, uint16_t)
DEFINE_PRINT_INT(print_u32, uint32_t)
DEFINE_PRINT_INT(print_u64, uint64_t)

void* allocate(size_t size) { return malloc(size); }
