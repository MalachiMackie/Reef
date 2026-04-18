#include <assert.h>
#include <corecrt_search.h>
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
    TypeId typeId;
}) ObjectHeader;

typedef PACK(struct {
    uint16_t variantIdentifier;
    int16_t offset;
}) VariablePlaceOffsetVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    int16_t offset;
}) VariablePlacePointerVariant;

typedef PACK(struct {
    union {
        uint16_t variantIdentifier;
        VariablePlaceOffsetVariant offsetInfo;
        VariablePlacePointerVariant pointerInfo;
    };
}) VariablePlace;

typedef PACK(struct {
    TypeId typeId;
    VariablePlace place;
    string name;
}) MethodParameter;

typedef PACK(struct {
    uint64_t length;
    MethodParameter items[];
}) MethodParameterArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    MethodParameterArray items;
}) MethodParameterArrayBoxedValue;

typedef PACK(struct {
    string name;
    TypeId typeId;
    uint16_t offset;
    uint16_t _padding;
}) FieldInfo;

typedef PACK(struct {
    string name;
    TypeId typeId;
    uint32_t _padding;
}) StaticFieldInfo;

typedef PACK(struct {
    uint64_t length;
    FieldInfo items[];
}) FieldInfoArray;

typedef PACK(struct {
    uint64_t length;
    StaticFieldInfo items[];
}) StaticFieldInfoArray;

typedef PACK(struct {
    TypeId typeId;
    uint32_t _padding;
    FieldInfoArray items;
}) FieldInfoArrayBoxedValue;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    StaticFieldInfoArray items;
}) StaticFieldInfoArrayBoxedValue;

typedef PACK(struct {
    string name;
    FieldInfoArrayBoxedValue *fields;
}) VariantInfo;

typedef PACK(struct {
    uint64_t length;
    VariantInfo items[];
}) VariantInfoArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    VariantInfoArray items;
}) VariantInfoArrayBoxedValue;

typedef PACK(struct {
    uint16_t variantIdentifier;
    uint16_t _padding;
    uint32_t __padding;
    string fullyQualifiedName;
    string name;
    TypeId typeId;
    uint32_t ___padding;
    StaticFieldInfoArrayBoxedValue *staticFields;
    FieldInfoArrayBoxedValue *fields;
}) TypeInfoClassVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    uint16_t _padding;
    uint32_t __padding;
    string fullyQualifiedName;
    string name;
    TypeId typeId;
    uint32_t ___padding;
    StaticFieldInfoArrayBoxedValue *staticFields;
    VariantInfoArrayBoxedValue *variants;
    struct {
        void* functionReference;
        void* functionParameter;
    } variantIdentifierGetter;
}) TypeInfoUnionVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    uint16_t _padding;
    uint32_t __padding;
    string fullyQualifiedName;
    TypeId pointerToTypeId;
    uint32_t ___padding;
}) TypeInfoPointerVariant;

typedef PACK(struct {
    uint16_t variantIdentifier;
    uint16_t _padding;
    uint32_t __padding;
    string fullyQualifiedName;
    TypeId elementTypeId;
    uint64_t length;
    uint8_t isDynamic;
}) TypeInfoArrayVariant;

typedef PACK(struct {
    union {
        uint16_t variantIdentifier;
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
    int16_t stackOffset;
    uint16_t _padding;
}) MethodLocal;

typedef PACK(struct {
    uint64_t length;
    MethodLocal locals[];
}) MethodLocalArray;

typedef PACK(struct {
    ObjectHeader objectHeader;
    uint32_t _padding;
    MethodLocalArray locals;
}) LocalsBoxedValue;

typedef PACK(struct {
    uint32_t methodId;
    uint32_t _padding;
    string fullyQualifiedName;
    string name;
    MethodParameterArrayBoxedValue *parameters;
    LocalsBoxedValue *locals;
    uint64_t addressFrom;
    uint64_t addressTo;
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
    print_string(handle->fullyQualifiedName);
    fputs(":\n    id: ", stdout);
    print_u32(handle->methodId);

    fputs("\n    display_name: ", stdout);
    print_string(handle->name);

    fputs("\n    address_from: 0x", stdout);
    print_size_t_hex(handle->addressFrom);

    fputs("\n    address_to: 0x", stdout);
    print_size_t_hex(handle->addressTo);

    fputs("\n    parameters: \n            length: ", stdout);
    MethodParameterArray* parameters = &handle->parameters->items;
    print_u64(parameters->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < parameters->length; i++)
    {
        if (i > 0)
        {
            fputs("\n", stdout);
        }
        MethodParameter parameter = parameters->items[i];
        fputs("            - name: ", stdout);
        print_string(parameter.name);
        fputs("\n              typeId: ", stdout);
        print_u32(parameter.typeId);
        fputs("\n              place:\n                variant: ", stdout);
        switch (parameter.place.variantIdentifier)
        {
            case 0:
                fputs("Offset", stdout);
                fputs("\n                offset: ", stdout);
                print_u16(parameter.place.offsetInfo.offset);
                break;
            case 1:
                fputs("Pointer", stdout);
                fputs("\n                pointerPlace: ", stdout);
                print_u16(parameter.place.pointerInfo.offset);
                break;
            default:
                assert(0);
                break;
        }
    }
    fputs("\n    locals: \n            length: ", stdout);
    MethodLocalArray* locals = &handle->locals->locals;
    print_u64(locals->length);
    fputs("\n", stdout);
    for (size_t i = 0; i < locals->length; i++)
    {
        if (i > 0)
        {
            fputs("\n", stdout);
        }
        fputs("            - typeId: ", stdout);
        print_u32(locals->locals[i].typeId);
        fputs("\n              offset: ", stdout);
        print_i16(locals->locals[i].stackOffset);
    }
    fputs("\n", stdout);
}

void print_type_info(TypeInfo *handle)
{
    switch (handle->variantIdentifier)
    {
        case 0: // class
        {
            print_string(handle->classInfo.fullyQualifiedName);
            fputs(":\n    type: class\n    id: ", stdout);
            print_u32(handle->classInfo.typeId);
            fputs("\n    displayName: ", stdout);
            print_string(handle->classInfo.name);
            uint64_t fields_count = handle->classInfo.fields->items.length;
            fputs("\n    fields:\n        length: ", stdout);
            print_u64(fields_count);
            fputs("\n        items:", stdout);
            for (uint64_t i = 0; i < fields_count; i++)
            {
                if (i > 0)
                {
                    fputs("\n", stdout);
                }
                FieldInfo field = handle->classInfo.fields->items.items[i];
                fputs("\n            - name: ", stdout);
                print_string(field.name);
                fputs("\n              type_id: ", stdout);
                print_u32(field.typeId);
                fputs("\n              offset: ", stdout);
                print_u16(field.offset);
            }
            fputs("\n", stdout);
            break;
        }
        case 1: // union
        {
            print_string(handle->unionInfo.fullyQualifiedName);
            fputs(":\n    type: union\n    id: ", stdout);
            print_u32(handle->unionInfo.typeId);
            fputs("\n    displayName: ", stdout);
            print_string(handle->classInfo.name);
            fputs("\n    variants_count: ", stdout);
            print_u64(handle->unionInfo.variants->items.length);
            fputs("\n", stdout);
            break;
        }
        case 2: // pointer
        {
            print_string(handle->pointerInfo.fullyQualifiedName);
            fputs(":\n    type: pointer\n", stdout);
            break;
        }
        case 3: // array
        {
            print_string(handle->arrayInfo.fullyQualifiedName);
            fputs(":\n    type: array\n    is_dynamic: ", stdout);
            if (handle->arrayInfo.isDynamic == 0)
            {
                fputs("false", stdout);
            }
            else
            {
                fputs("true", stdout);
            }
            fputs("\n", stdout);
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

extern void* get_rbp();

MethodInfo* try_get_reef_method_by_instruction_address(uint64_t address)
{
    for (uint32_t i = 0; i < methodInfoCount; i++)
    {
        MethodInfo* method = &methodInfoArray[i];
        if (address > method->addressFrom && address < method->addressTo)
        {
            return method;
        }
    }

    return NULL;
}

typedef void(*traverse_stack_fn)(uint16_t depth, uint64_t instructionAddress, MethodInfo* method, void* data);

void traverse_stack(traverse_stack_fn fn, void* data)
{
    uint64_t* nextRbp = get_rbp();
    uint64_t returnAddress = *(nextRbp + 1);
    MethodInfo* reefMethod = try_get_reef_method_by_instruction_address(returnAddress);
    uint16_t depth = 0;
    while (reefMethod != NULL)
    {
        fn(depth, returnAddress, reefMethod, data);
        nextRbp = (uint64_t*)*nextRbp;
        returnAddress = *(nextRbp + 1);
        reefMethod = try_get_reef_method_by_instruction_address(returnAddress);
        depth++;
    }
}

void print_method(uint16_t depth, uint64_t instructionAddress, MethodInfo* method, void* data)
{
    if (depth > 0)
    {
        fputs("\n", stdout);
    }
    print_string(method->fullyQualifiedName);
}

void print_stack_trace()
{
    traverse_stack(&print_method, NULL);
}

void trigger_gc() {

}

size_t get_memory_usage_bytes() {
    return memory_used_bytes;
}
