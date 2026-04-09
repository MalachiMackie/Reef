#include <assert.h>
#include <stdalign.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>

#ifdef _MSC_VER
typedef double max_align_t;
#endif

#ifdef __GNUC__
#define PACK( __Declaration__ ) __Declaration__ __attribute__((__packed__))
#endif

#ifdef _MSC_VER
#define PACK( __Declaration__ ) __pragma( pack(push, 1) ) __Declaration__ __pragma( pack(pop))
#endif

void print_i8(int8_t num);
void print_i16(int16_t num);
void print_i32(int32_t num);
void print_i64(int64_t num);

void print_u8(uint8_t num);
void print_u16(uint16_t num);
void print_u32(uint32_t num);
void print_u64(uint64_t num);

typedef struct {
	size_t length;
	const char *start;
} string;

typedef uint32_t TypeId;

typedef PACK(struct {
    string name;
    TypeId typeId;
    uint32_t _padding;
}) FieldInfo;

typedef PACK(struct {
    uint64_t length;
    FieldInfo items[10];
}) FieldInfoArray;

typedef PACK(struct {
    string name;
    FieldInfoArray fields;
}) VariantInfo;

typedef PACK(struct {
    uint64_t length;
    VariantInfo items[10];
}) VariantInfoArray;

typedef PACK(struct {
    string name;
    TypeId typeId;
    uint32_t _padding;
    FieldInfoArray staticFields;
    FieldInfoArray fields;
}) TypeInfoClassVariant;

typedef PACK(struct {
    string name;
    TypeId typeId;
    uint32_t _padding;
    FieldInfoArray staticFields;
    VariantInfoArray variants;
    struct {
        void* functionReference;
        void* functionParameter;
    } variantIdentifierGetter;
}) TypeInfoUnionVariant;

typedef PACK(struct {
    TypeId pointerToTypeId;
    uint32_t _padding;
}) TypeInfoPointerVariant;

typedef PACK(struct {
    TypeId elementTypeId;
    uint32_t _padding;
    uint64_t length;
}) TypeInfoArrayVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    uint16_t __padding;
    uint32_t _padding;
    union {
        TypeInfoClassVariant classInfo;
        TypeInfoUnionVariant unionInfo;
        TypeInfoPointerVariant pointerInfo;
        TypeInfoArrayVariant arrayInfo;
    };
}) TypeInfo;

extern TypeInfo typeInfoArray[];

typedef PACK(struct {
    uint64_t length;
    TypeId typeIds[];
}) TypeIdArray;

typedef PACK(struct {
    TypeId typeId;
    uint32_t _padding;
    TypeIdArray parameters;
}) ParametersBoxedValue;

typedef PACK(struct {
    TypeId typeId;
    uint32_t _padding;
    TypeIdArray locals;
}) LocalsBoxedValue;

typedef PACK(struct {
    uint32_t methodId;
    uint32_t _padding;
    string name;
    ParametersBoxedValue *parameters;
    LocalsBoxedValue *locals;
}) MethodInfo;
extern MethodInfo methodInfoArray[];

extern uint64_t methodInfoCount;
extern uint64_t methodInfoSize;
extern uint64_t typeInfoCount;
extern uint64_t typeInfoSize;
extern uint64_t fieldInfoSize;
extern uint64_t variantInfoSize;

MethodInfo *get_method_info(uint64_t index)
{
    assert(index < methodInfoCount);
    return &methodInfoArray[index];
}

TypeInfo *get_type_info(uint64_t index)
{
    assert(index < typeInfoCount);

    return &typeInfoArray[index];
}

void print_string(string str)
{
	fputs(str.start, stdout);
}

string get_type_info_name(TypeInfo *handle)
{
    switch (handle->variantIdentifier)
    {
        case 0: // class
            return handle->classInfo.name;
        case 1: // union
            return handle->unionInfo.name;
        case 2: // pointer
        {
            TypeInfo* pointer_to_handle = get_type_info(handle->pointerInfo.pointerToTypeId);
            string value;

            // todo: actually get name
            value.length = 6;
            value.start = "*thing";
            return value;
        }
        case 3: // array
        {
            // todo: actually get name
            string value;
            value.length = 10;
            value.start = "[thing; 1]";
            return value;
        }
        default:
            assert(0);
    }
}

#define DEFINE_PRINT_INT(func_name, int_type)                            \
void func_name(int_type num)                                             \
{                                                                        \
    /* Handle negative numbers safely */                                 \
    if (num < 0)                                                         \
    {                                                                    \
        fputc('-', stdout);                                              \
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
    fputc('0' + (int)num, stdout);                                       \
}

DEFINE_PRINT_INT(print_i8, int8_t)
DEFINE_PRINT_INT(print_i16, int16_t)
DEFINE_PRINT_INT(print_i32, int32_t)
DEFINE_PRINT_INT(print_i64, int64_t)

DEFINE_PRINT_INT(print_u8, uint8_t)
DEFINE_PRINT_INT(print_u16, uint16_t)
DEFINE_PRINT_INT(print_u32, uint32_t)
DEFINE_PRINT_INT(print_u64, uint64_t)

void print_size_t_hex(size_t value)
{
    int started = 0;

    for (int i = (int)(sizeof(size_t) * 2) - 1; i >= 0; --i)
    {
        unsigned digit = (value >> (i * 4)) & 0xF;

        if (digit != 0 || started || i == 0)
        {
            started = 1;
            putchar(digit < 10 ? '0' + digit : 'A' + (digit - 10));
        }
    }
}

void print_method_info(MethodInfo* handle)
{
    print_string(handle->name);
    fputs(":\n    id: ", stdout);
    print_u32(handle->methodId);

    fputs("\n    parameters: \n            length: ", stdout);
    TypeIdArray* parameters = &handle->parameters->parameters;
    print_u64(parameters->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < parameters->length; i++)
    {
        fputs("            [", stdout);
        print_u64(i);
        fputs("]: ", stdout);
        print_u32(parameters->typeIds[i]);
        fputs("\n", stdout);
    }

    fputs("\n    locals: \n            length: ", stdout);
    TypeIdArray* locals = &handle->locals->locals;
    print_u64(locals->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < locals->length; i++)
    {
        fputs("            [", stdout);
        print_u64(i);
        fputs("]: ", stdout);
        print_u32(locals->typeIds[i]);
        fputs("\n", stdout);
    }
}

void print_type_info(TypeInfo *handle)
{
    switch (handle->variantIdentifier)
    {
        case 0: // class
        {
            print_string(handle->classInfo.name);
            fputs(":\n    type: class\n", stdout);
            fputs("    id: ", stdout);
            print_u32(handle->classInfo.typeId);
            fputs("\n", stdout);
            break;
        }
        case 1: // union
        {
            print_string(handle->unionInfo.name);
            fputs(":\n    type: union\n", stdout);
            fputs("    id: ", stdout);
            print_u32(handle->unionInfo.typeId);
            fputs("\n", stdout);
            break;
        }
        case 2: // pointer
        {
            print_string(get_type_info_name(handle));
            fputs(":\n    type: pointer\n", stdout);
            break;
        }
        case 3: // array
        {
            print_string(get_type_info_name(handle));
            fputs(":\n    type: array\n", stdout);
            break;
        }
    }
}

size_t memory_used_bytes = 0;

size_t heap_size_bytes = 1024 * 10; // 10MB

typedef struct {
    void* ptr;
    size_t size;
    uint8_t allocation_space;
} Allocation;

typedef struct {
    Allocation* allocations;
    size_t count;
    size_t size;
} AllocationsList;

typedef struct {
    void* base_addr;
    void* top;
    size_t size;
    AllocationsList allocations;
} Heap;

Heap* heaps;
size_t heaps_count;

void init_runtime() {
    assert(typeInfoSize == sizeof(TypeInfo));
    assert(methodInfoSize == sizeof(MethodInfo));

    heaps_count = 1;
    heaps = malloc(heaps_count * sizeof(Heap));
    assert(heaps);

    AllocationsList allocations;

    allocations.size = 10;
    allocations.count = 0;
    allocations.allocations = malloc(allocations.size * sizeof(Allocation));
    assert(allocations.allocations);

    Heap heap;
    heap.size = heap_size_bytes;
    heap.base_addr = malloc(heap.size);
    heap.top = heap.base_addr;
    assert(heap.base_addr);
    heap.allocations = allocations;

    heaps[0] = heap;

}

void* allocate(size_t size) {
    size_t old_top = (size_t)heaps[0].top;
    size_t unaligned_new_top = old_top + size;
    size_t new_top = unaligned_new_top + (unaligned_new_top % alignof(max_align_t));

    memory_used_bytes += new_top - old_top;

    // todo: if no more space in heap, allocate a new one
    assert(new_top < (size_t)heaps[0].base_addr + heaps[0].size);
    heaps[0].top = (void*)new_top;
    AllocationsList* allocations = &heaps[0].allocations;

    // todo: if no more allocations, allocate a new one
    assert(allocations->count < allocations->size);

    Allocation allocation;
    allocation.ptr = (void*)old_top;
    allocation.size = size;
    allocation.allocation_space = (uint8_t)(old_top - new_top);

    allocations->allocations[allocations->count++] = allocation;

    return (void*)old_top;
}

void print_all_types()
{
    for (uint64_t i = 0; i < typeInfoCount; i++)
    {
        print_type_info(get_type_info(i));
    }
}

void print_all_methods()
{
    for (uint64_t i = 0; i < methodInfoCount; i++)
    {
        print_method_info(get_method_info(i));
    }
}

void trigger_gc() {

}

size_t get_memory_usage_bytes() {
    return memory_used_bytes;
}
